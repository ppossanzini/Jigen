using Jigen.Calibration;
using Jigen.Client.BaseTypes;

namespace Jigen.Client;

/// <summary>
/// A search hit from a specific collection/modality, carrying both its raw
/// similarity and the cross-modal calibrated score used for the merged ranking.
/// </summary>
public sealed class CalibratedSearchResult<T>
  where T : class, new()
{
  /// <summary>The collection/modality the hit comes from (e.g. "image", "text").</summary>
  public string Modality { get; init; }

  /// <summary>The original search result from the collection.</summary>
  public VectorSearchResult<T> Result { get; init; }

  /// <summary>The raw similarity returned by the server (not comparable across modalities).</summary>
  public float RawScore => Result.Score;

  /// <summary>
  /// The z-scored value of <see cref="RawScore"/> within its modality group
  /// (mean 0, std 1): comparable across modalities, used to order the merge.
  /// </summary>
  public float CalibratedScore { get; init; }
}

/// <summary>
/// Cross-modal helpers for the .NET client: search each collection separately
/// (each with its own tuning) and merge the rankings on a common scale using
/// <see cref="CrossModalScoreCalibrator"/> — the z-score per collection makes
/// an image and a text that are equally "best in their group" rank together.
/// The raw scores are never comparable across modalities (text↔text cosines
/// dwarf image↔text ones), so merging them directly would always bury one
/// modality under the other.
/// </summary>
public static class CrossModalSearch
{
  /// <summary>
  /// Merges the results of multiple collections/modalities into a single
  /// ranking calibrated per group.
  /// </summary>
  /// <typeparam name="T">The document type of the searched collections.</typeparam>
  /// <param name="groups">
  /// One entry per collection/modality: the modality label and its (already
  /// searched and score-sorted) results.
  /// </param>
  /// <returns>
  /// All hits, ordered by descending <see cref="CalibratedSearchResult{T}.CalibratedScore"/>.
  /// Groups with a single hit or zero variance map to a calibrated score of 0
  /// (no ranking information within the group).
  /// </returns>
  public static List<CalibratedSearchResult<T>> MergeCalibrated<T>(params (string Modality, IReadOnlyList<VectorSearchResult<T>> Results)[] groups)
    where T : class, new()
  {
    ArgumentNullException.ThrowIfNull(groups);

    var flattened = new List<(string Modality, VectorSearchResult<T> Result)>();
    foreach (var (modality, results) in groups)
    {
      if (results is null)
        continue;
      foreach (var result in results)
        flattened.Add((modality, result));
    }

    if (flattened.Count == 0)
      return [];

    var calibrated = CrossModalScoreCalibrator.Calibrate(
      flattened.Select(f => f.Modality).ToArray(),
      flattened.Select(f => f.Result.Score).ToArray());

    return flattened
      .Select((f, index) => new CalibratedSearchResult<T>
      {
        Modality = f.Modality,
        Result = f.Result,
        CalibratedScore = calibrated[index]
      })
      .OrderByDescending(r => r.CalibratedScore)
      .ToList();
  }
}
