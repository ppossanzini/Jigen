using System.Numerics;
using System.Runtime.InteropServices;

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
{
  private readonly Memory<T> _buffer;

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

  public long Count
  {
    get => Volatile.Read(ref _tail) - Volatile.Read(ref _head);
  }

  public bool IsEmpty => Count == 0;


  public async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
  {
    await _freeSlots.WaitAsync(cancellationToken);

    try
    {
      var position = (int)((Interlocked.Increment(ref _tail) - 1) & _capacityMask);
      _buffer.Span[position] = item;
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
      _buffer.Span[position] = item;
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
      var position = (int)(_head & _capacityMask);
      var result = _buffer.Span[position];
      _buffer.Span[position] = default!;

      Interlocked.Increment(ref _head);
      return result;
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
      var position = (int)(_head & _capacityMask);
      result = _buffer.Span[position];
      _buffer.Span[position] = default!;
      Interlocked.Increment(ref _head);
      return true;
    }
    finally
    {
      _freeSlots.Release();
    }
  }

  public T Peek()
  {
    var position = (int)(_head & _capacityMask);
    return _buffer.Span[position];
  }
}