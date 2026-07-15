// <copyright file="BinaryHeap.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// Rewritten for zero-allocation hot path: T[] backing store, no IList<T> dispatch,
// no LINQ. SiftUp/SiftDown use direct array indexing with ref-swap.
// Comparison<T> drives the ordering: converting IComparer<T>.Compare to a delegate
// at construction time devirtualizes the interface dispatch. The Comparison<T>
// constructor / Initialize overload lets the hot path avoid the ReverseComparer
// wrapper entirely.
// Poolable: parameterless constructor + Initialize + Clear let the indexer
// recycle heap instances across SEARCH-LAYER calls without re-allocating buffers.

using System.Runtime.CompilerServices;

namespace Jigen.Indexer
{
  /// <summary>
  /// Binary max-heap backed by a plain array.
  /// The maximum element is always at index 0 (top).
  /// Order is driven by a <see cref="Comparison{T}"/> delegate; the
  /// <see cref="IComparer{T}"/> overloads capture its Compare method
  /// into a delegate so all comparisons go through a single non-virtual call.
  /// Supports pooling: call <see cref="Initialize"/> to reuse a cleared heap.
  /// </summary>
  public class BinaryHeap<T>
  {
    private T[] _buffer;
    private int _count;
    private Comparison<T> _comparison;

    /// <summary>For pooling: creates an uninitialized heap — call <see cref="Initialize"/> before use.</summary>
    public BinaryHeap() { }

    /// <summary>Creates an empty heap with the given initial capacity.</summary>
    public BinaryHeap(IComparer<T> comparer, int initialCapacity = 8)
    {
      var c = comparer ?? Comparer<T>.Default;
      Comparer = c;
      _comparison = c.Compare; // captures concrete method — devirtualized
      _buffer = new T[Math.Max(initialCapacity, 4)];
    }

    /// <summary>Creates an empty heap driven by a <see cref="Comparison{T}"/> delegate.</summary>
    public BinaryHeap(Comparison<T> comparison, int initialCapacity = 8)
    {
      _comparison = comparison ?? Comparer<T>.Default.Compare;
      _buffer = new T[Math.Max(initialCapacity, 4)];
    }

    /// <summary>
    /// Creates a heap from an existing list (elements are copied and heapified).
    /// The source list is not modified.
    /// </summary>
    public BinaryHeap(IList<T> source, IComparer<T> comparer)
    {
      var c = comparer ?? Comparer<T>.Default;
      Comparer = c;
      _comparison = c.Compare;
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

    /// <summary>
    /// The <see cref="IComparer{T}"/> that was originally passed, if any.
    /// Null when constructed from a <see cref="Comparison{T}"/> directly.
    /// </summary>
    public IComparer<T> Comparer { get; private set; }

    /// <summary>
    /// Reinitialises a pooled (or default-constructed) heap with a new comparer
    /// and the requested minimum capacity. Existing elements are discarded.
    /// </summary>
    public void Initialize(IComparer<T> comparer, int minCapacity = 8)
    {
      var c = comparer ?? Comparer<T>.Default;
      Comparer = c;
      _comparison = c.Compare;
      EnsureCapacity(minCapacity);
      _count = 0;
    }

    /// <summary>
    /// Reinitialises a pooled heap with a <see cref="Comparison{T}"/> delegate
    /// — the hot-path overload that avoids <see cref="ReverseComparer{T}"/>
    /// allocation and double virtual dispatch.
    /// </summary>
    public void Initialize(Comparison<T> comparison, int minCapacity = 8)
    {
      _comparison = comparison ?? Comparer<T>.Default.Compare;
      Comparer = null;
      EnsureCapacity(minCapacity);
      _count = 0;
    }

    private void EnsureCapacity(int minCapacity)
    {
      var cap = Math.Max(minCapacity, 4);
      if (_buffer is null || _buffer.Length < cap)
        _buffer = new T[cap];
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Compare(T x, T y) => _comparison(x, y);

    private void SiftDown(int i)
    {
      while (true)
      {
        int l = (i << 1) + 1;
        if (l >= _count) break;
        int r = l + 1;
        int m = r < _count && Compare(_buffer[l], _buffer[r]) < 0 ? r : l;
        if (Compare(_buffer[m], _buffer[i]) <= 0) break;
        Swap(i, m);
        i = m;
      }
    }

    private void SiftUp(int i)
    {
      while (i > 0)
      {
        int p = (i - 1) >> 1;
        if (Compare(_buffer[i], _buffer[p]) <= 0) break;
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
