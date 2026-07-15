// <copyright file="BinaryHeap.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// Rewritten for zero-allocation hot path: T[] backing store, no IList<T> dispatch,
// no LINQ. SiftUp/SiftDown use direct array indexing with ref-swap.
// Poolable: parameterless constructor + Initialize + Clear let the indexer
// recycle heap instances across SEARCH-LAYER calls without re-allocating buffers.

using System.Runtime.CompilerServices;

namespace Jigen.Indexer
{
  /// <summary>
  /// Binary max-heap backed by a plain array.
  /// The maximum element is always at index 0 (top).
  /// Order is customizable via <see cref="IComparer{T}"/>.
  /// Supports pooling: call <see cref="Initialize"/> to reuse a cleared heap.
  /// </summary>
  public class BinaryHeap<T>
  {
    private T[] _buffer;
    private int _count;

    /// <summary>For pooling: creates an uninitialized heap — call <see cref="Initialize"/> before use.</summary>
    public BinaryHeap() { }

    /// <summary>Creates an empty heap with the given initial capacity.</summary>
    public BinaryHeap(IComparer<T> comparer, int initialCapacity = 8)
    {
      Comparer = comparer ?? Comparer<T>.Default;
      _buffer = new T[Math.Max(initialCapacity, 4)];
    }

    /// <summary>
    /// Creates a heap from an existing list (elements are copied and heapified).
    /// The source list is not modified.
    /// </summary>
    public BinaryHeap(IList<T> source, IComparer<T> comparer)
    {
      Comparer = comparer ?? Comparer<T>.Default;
      _count = source.Count;
      if (_count > 0)
      {
        _buffer = new T[_count];
        for (int i = 0; i < _count; i++) _buffer[i] = source[i];
        for (int i = 1; i < _count; i++) SiftUp(i);
      }
      else
      {
        _buffer = new T[4];
      }
    }

    public IComparer<T> Comparer { get; private set; }

    /// <summary>
    /// Reinitialises a pooled (or default-constructed) heap with a new comparer
    /// and the requested minimum capacity. Existing elements are discarded.
    /// </summary>
    public void Initialize(IComparer<T> comparer, int minCapacity = 8)
    {
      Comparer = comparer ?? Comparer<T>.Default;
      var cap = Math.Max(minCapacity, 4);
      if (_buffer is null || _buffer.Length < cap)
        _buffer = new T[cap];
      _count = 0;
    }

    /// <summary>
    /// Empties the heap and nulls references if <typeparamref name="T"/> is
    /// a reference type, so pooled heaps don't keep dead objects alive.
    /// </summary>
    public void Clear()
    {
      if (_count > 0 && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        Array.Clear(_buffer, 0, _count);
      _count = 0;
    }

    /// <summary>Number of elements currently in the heap.</summary>
    public int Count => _count;

    /// <summary>True when the heap contains no elements.</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>Returns the top (maximum) element without removing it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Peek() => _buffer[0];

    /// <summary>Pushes an item onto the heap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
      if (_count == _buffer.Length) Grow();
      _buffer[_count] = item;
      SiftUp(_count);
      _count++;
    }

    /// <summary>Removes and returns the top (maximum) element.</summary>
    public T Pop()
    {
      if (_count == 0) throw new InvalidOperationException("Heap is empty");

      var result = _buffer[0];
      _count--;
      if (_count > 0)
      {
        _buffer[0] = _buffer[_count];
        _buffer[_count] = default!;
        SiftDown(0);
      }
      else
      {
        _buffer[0] = default!;
      }

      return result;
    }

    /// <summary>
    /// Returns a new <see cref="List{T}"/> containing all heap elements
    /// in their current (heap) order.
    /// </summary>
    public List<T> ToList()
    {
      var list = new List<T>(_count);
      for (int i = 0; i < _count; i++) list.Add(_buffer[i]);
      return list;
    }

    private void Grow()
    {
      var newBuffer = new T[_buffer.Length * 2];
      Array.Copy(_buffer, newBuffer, _count);
      _buffer = newBuffer;
    }

    private void SiftDown(int i)
    {
      while (true)
      {
        int l = (i << 1) + 1;
        if (l >= _count) break;
        int r = l + 1;
        int m = r < _count && Comparer.Compare(_buffer[l], _buffer[r]) < 0 ? r : l;
        if (Comparer.Compare(_buffer[m], _buffer[i]) <= 0) break;
        Swap(i, m);
        i = m;
      }
    }

    private void SiftUp(int i)
    {
      while (i > 0)
      {
        int p = (i - 1) >> 1;
        if (Comparer.Compare(_buffer[i], _buffer[p]) <= 0) break;
        Swap(i, p);
        i = p;
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Swap(int i, int j)
    {
      (_buffer[i], _buffer[j]) = (_buffer[j], _buffer[i]);
    }
  }
}
