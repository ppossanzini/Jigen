using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable MemberCanBePrivate.Global

namespace Jigen.PerformancePrimitives;

/// <summary>
/// Circular memory queue implementation with fixed capacity 
/// provides thread-safe enqueue and dequeue operations for a fixed-size buffer.
/// It mimics Circular Buffer data structure from Hadoop
/// 
/// It creates a fixed contiguos memory array of pointer of T.
/// 
/// Writing threads add ref to an object in the next free position using a
/// Tail counter and calculate the current free position using _tail & capacity operation.   
/// 
/// Reading thread follows writing threads in a similar way using a Head counter and _head % capacity operation.
/// 
/// Objects are already in heap memory and do not need to be copied. Interlocks are used to ensure thread safety.
/// 
/// Semaphores are used to manage available buffer positions and free slots, ensuring thread-safe enqueue and dequeue operations
/// and avoiding reading threads go ahead of writing threads.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CircularMemoryQueue<T>
where T : class
{
  // Plain array, not Memory<T>: every Memory<T>.Span access performs a type
  // check on the wrapped object and constructs a new Span — ~2x the cost of a
  // direct array access, paid inside the spin loops.
  private readonly T[] _buffer;

  private long _tail;
  private long _head;

  private readonly int _capacity;
  private readonly int _capacityMask;
  private readonly SemaphoreSlim _freeSlots;
  private readonly SemaphoreSlim _availableItems;


  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="capacity">Buffer size must be >0 and will be changed to the nearest power of 2 for efficient modulo operations</param>
  /// <exception cref="ArgumentException">If capacity is less than 512</exception>
  public CircularMemoryQueue(uint capacity = 1_000_000)
  {
    if (capacity < 512) throw new ArgumentException("Capacity must be >= 512");

    _capacity = (int)BitOperations.RoundUpToPowerOf2(capacity);
    _capacityMask = _capacity - 1;

    _buffer = new T[_capacity];
    _freeSlots = new SemaphoreSlim(_capacity, _capacity);
    _availableItems = new SemaphoreSlim(0, _capacity);
  }


  public int Length => _capacity;

  public long Count => Volatile.Read(ref _tail) - Volatile.Read(ref _head);

  public bool IsEmpty => Count == 0;

  public async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
  {
    await _freeSlots.WaitAsync(cancellationToken);

    try
    {
      var position = (int)((Interlocked.Increment(ref _tail) - 1) & _capacityMask);
      PublishToSlot(position, item);
    }
    finally
    {
      _availableItems.Release();
    }
  }

  public void Enqueue(T item)
  {
    _freeSlots.Wait();

    try
    {
      var position = (int)((Interlocked.Increment(ref _tail) - 1) & _capacityMask);
      PublishToSlot(position, item);
    }
    finally
    {
      _availableItems.Release();
    }
  }


  public T Dequeue(CancellationToken cancellationToken = default)
  {
    _availableItems.Wait(cancellationToken);

    try
    {
      var position = (int)((Interlocked.Increment(ref _head) - 1) & _capacityMask);
      return TakeFromSlot(position);
    }
    finally
    {
      _freeSlots.Release();
    }
  }

  public bool TryDequeue(out T result)
  {
    result = default;
    if (!_availableItems.Wait(0))
      return false;

    try
    {
      var position = (int)((Interlocked.Increment(ref _head) - 1) & _capacityMask);
      result = TakeFromSlot(position);
      return true;
    }
    finally
    {
      _freeSlots.Release();
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void PublishToSlot(int position, T item)
  {
    // The slot may still hold the previous lap's item (its consumer was
    // preempted), and because semaphore permits are fungible TWO producers one
    // lap apart can even target the same slot concurrently. A check-then-write
    // would let the second overwrite the first (lost item, stuck consumer):
    // the CAS claims the slot atomically only when it is actually empty.
    var spinner = new SpinWait();
    while (Interlocked.CompareExchange(ref _buffer[position], item, null) is not null)
      spinner.SpinOnce();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private T TakeFromSlot(int position)
  {
    // The producer that reserved this slot may not have published yet
    // (preempted between the _tail reservation and the write, while another
    // producer released _availableItems): wait for the item to appear.
    // Exchange takes and clears the slot atomically, so a delayed consumer
    // can never clobber an item written by the next-lap producer.
    var spinner = new SpinWait();
    T result;
    while ((result = Interlocked.Exchange(ref _buffer[position], null)) is null)
      spinner.SpinOnce();

    return result;
  }

  public T Peek()
  {
    var position = (int)(Interlocked.Read(ref _head) & _capacityMask);
    return Volatile.Read(ref _buffer[position]);
  }
}