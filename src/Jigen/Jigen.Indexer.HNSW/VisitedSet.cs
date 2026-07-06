using System.Runtime.CompilerServices;

namespace Jigen.Indexer;

/// <summary>
/// Visited-set for graph traversal using epoch stamping: a slot is "visited"
/// when it carries the current epoch, so starting a new traversal is a single
/// counter increment instead of clearing nodeCount bytes. On large graphs the
/// per-search memset was the dominant fixed cost of SEARCH-LAYER
/// (~nodeCount bytes × levels × operations).
/// Not thread-safe: rent one per traversal from the indexer's pool.
/// </summary>
internal sealed class VisitedSet
{
  private int[] _epochs = [];
  private int _epoch;

  /// <summary>Starts a new traversal over ids in [0, minSize).</summary>
  public void Prepare(int minSize)
  {
    if (_epochs.Length < minSize)
    {
      // Fresh array is already all-zero: epoch 1 marks nothing as visited.
      _epochs = new int[Math.Max(minSize, _epochs.Length * 2)];
      _epoch = 1;
      return;
    }

    if (++_epoch == int.MaxValue)
    {
      // Epoch wrap (once every ~2 billion traversals): reset stamps.
      Array.Clear(_epochs);
      _epoch = 1;
    }
  }

  /// <summary>
  /// Marks <paramref name="id"/> visited. Returns false when it was already
  /// visited in this traversal, or lies outside the prepared range (nodes
  /// inserted concurrently, not fully wired yet: skipped, like before).
  /// </summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool TryVisit(int id)
  {
    if ((uint)id >= (uint)_epochs.Length) return false;
    if (_epochs[id] == _epoch) return false;

    _epochs[id] = _epoch;
    return true;
  }
}
