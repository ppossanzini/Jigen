namespace Jigen.Calibration;

/// <summary>
/// Makes raw similarity scores from different modalities comparable for
/// ranking, using the candidate set's own statistics.
///
/// In a mixed collection (images + texts) the raw cosine scores of the two
/// modality groups live on different scales, so a text query's results are
/// always dominated by the text group (text↔text similarity is far higher than
/// image↔text). This calibrator z-scores each modality group independently:
/// within every group the scores are shifted and scaled to mean 0 / std 1, so
/// an image and a text that are equally "good" relative to their own group get
/// the same calibrated score and can be ranked together.
///
/// This is a score-space (ranking) calibration, complementary to the
/// embedding-space <see cref="ModalityGapCalibrator"/>: it is query-dependent
/// and uses the candidate set, but it never changes the stored vectors and it
/// preserves the within-group ordering exactly.
///
/// Typical usage: at QUERY time, on the search results — e.g. from the .NET
/// client when merging the rankings of separate collections/modalities.
/// </summary>
public static class CrossModalScoreCalibrator
{
  /// <summary>
  /// Calibrates <paramref name="scores"/> by z-scoring each modality group.
  /// </summary>
  /// <param name="modalities">One modality label per score (e.g. "image"/"text").</param>
  /// <param name="scores">The raw scores (e.g. cosine similarities).</param>
  /// <returns>
  /// Calibrated scores, same order as the inputs. Groups with a single element
  /// or zero variance are mapped to 0 (no ranking information within the group).
  /// </returns>
  public static float[] Calibrate(IReadOnlyList<string> modalities, IReadOnlyList<float> scores)
  {
    ArgumentNullException.ThrowIfNull(modalities);
    ArgumentNullException.ThrowIfNull(scores);

    if (modalities.Count != scores.Count)
      throw new ArgumentException("modalities and scores must have the same length.");

    var calibrated = new float[scores.Count];

    foreach (var group in modalities.Select((modality, index) => (modality, index)).GroupBy(g => g.modality, StringComparer.Ordinal))
    {
      var indices = group.Select(g => g.index).ToArray();

      var mean = indices.Average(i => scores[i]);
      var variance = indices.Sum(i => (scores[i] - mean) * (scores[i] - mean)) / indices.Length;
      var std = MathF.Sqrt(variance);

      if (std <= 1e-6f)
      {
        // No spread (single candidate, or all identical): no ranking signal.
        foreach (var i in indices)
          calibrated[i] = 0f;
        continue;
      }

      foreach (var i in indices)
        calibrated[i] = (scores[i] - mean) / std;
    }

    return calibrated;
  }
}
