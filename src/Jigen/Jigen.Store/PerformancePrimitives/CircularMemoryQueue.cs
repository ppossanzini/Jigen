using System.Runtime.InteropServices;

namespace Jigen.PerformancePrimitives;

public class CircularMemoryQueue<T>(int capacity = 1024)
{
  private readonly Memory<T> _buffer = new T[capacity];

  private long _tail;
  private long _head;

  private readonly SemaphoreSlim _availableBufferPositions = new(capacity, capacity);

  private readonly SemaphoreSlim _freeSlots = new(capacity, capacity);
  private readonly SemaphoreSlim _availableItems = new(0, capacity);

  public int Length => capacity;

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
      var position = (int)((Interlocked.Increment(ref _tail) - 1) % capacity);
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
      var position = (int)((Interlocked.Increment(ref _tail) - 1) % capacity);
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
      var position = (int)(_head % capacity);
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
      var position = (int)(_head % capacity);
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
    var position = (int)(_head % capacity);
    return _buffer.Span[position];
  }
}