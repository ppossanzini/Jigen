using Jigen.Client.BaseTypes;

namespace Jigen.Client;

/// <summary>
/// Cross-modal merge as an extension method on the search results, so callers
/// do not need to build labelled groups: every <see cref="IEnumerable{T}"/>
/// passed to <see cref="MergeAndCalibrate{T}"/> is treated as one
/// collection/modality and z-scored independently, then all hits are merged
/// into a single ranking on the common calibrated scale.
/// </summary>
public static class VectorSearchResultExtensions
{
  /// <summary>
  /// Merges this result set with the additional ones and re-calibrates the
  /// scores with a per-group z-score (each <see cref="IEnumerable{T}"/> is one
  /// group), so results from different collections/modalities can be ranked
  /// together despite living on different raw similarity scales.
  /// </summary>
  /// <typeparam name="T">The document type of the searched collections.</typeparam>
  /// <param name="first">The receiver: the first group (e.g. the image hits).</param>
  /// <param name="others">Additional groups (e.g. the text hits).</param>
  /// <returns>
  /// All hits, ordered by descending <see cref="CalibratedSearchResult{T}.CalibratedScore"/>.
  /// <see cref="CalibratedSearchResult{T}.Modality"/> carries the group's
  /// zero-based position in the call ("0" = the receiver, "1" = the first
  /// <paramref name="others"/> argument, ...). Groups with a single hit or
  /// zero variance map to a calibrated score of 0 (no ranking information
  /// within the group).
  /// </returns>
  public static List<CalibratedSearchResult<T>> MergeAndCalibrate<T>(
    this IEnumerable<VectorSearchResult<T>> first,
    params IEnumerable<VectorSearchResult<T>>[] others)
    where T : class, new()
  {
    ArgumentNullException.ThrowIfNull(first);

    // Null groups are skipped BEFORE labelling, so Modality always reflects
    // the position among the meaningful (non-null) groups.
    var groups = new[] { first }
      .Concat(others ?? Array.Empty<IEnumerable<VectorSearchResult<T>>>())
      .Where(results => results is not null)
      .Select((results, index) => (Modality: index.ToString(), Results: results))
      .ToArray();

    return CrossModalSearch.MergeCore(groups);
  }
}
