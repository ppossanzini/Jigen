using Jigen.DataStructures;

namespace Jigen;

/// <summary>
/// Lexical index over sparse vectors: maps each term to the entries that carry
/// it, so a query sums the weights of the terms it shares with a document
/// without scanning the collection. Implementations keep every structure
/// derivable from the stored entries: the index is never the only owner of a
/// piece of data, and it can be rebuilt from the archive at any time.
/// </summary>
public interface ISparseIndex
{
  /// <summary>
  /// Adds or replaces the sparse vector of one entry. The collection is
  /// created on first use; re-adding the same key replaces its terms, so the
  /// operation is idempotent.
  /// </summary>
  void Add(string collection, byte[] key, SparseVector vector);

  /// <summary>Removes every term of one entry. Unknown keys are ignored.</summary>
  void Remove(string collection, byte[] key);

  /// <summary>Drops every term of a collection (collection cleared or dropped).</summary>
  void Clear(string collection);

  /// <summary>Drops every collection index, for a full rebuild from the archive.</summary>
  void ClearAll();

  /// <summary>
  /// Merges entries into the collection index. Used to rebuild from the archive:
  /// adding is idempotent, so a rebuild may overlap with concurrent writes.
  /// </summary>
  void Rebuild(string collection, IEnumerable<(byte[] Key, SparseVector Vector)> entries);

  /// <summary>True when the collection index has been built at least once.</summary>
  bool IsBuilt(string collection);

  /// <summary>
  /// Returns at most <paramref name="top"/> keys ordered by descending score,
  /// ties broken by ascending key, or an empty list when the query shares no
  /// term with the collection.
  /// </summary>
  IReadOnlyList<(byte[] Key, float Score)> Search(string collection, SparseVector query, int top);

  /// <summary>Metrics for one collection, all zeros when the index is absent.</summary>
  SparseIndexInfo GetInfo(string collection);
}
