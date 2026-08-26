namespace Jigen.Calibration;

/// <summary>
/// Estimates and corrects the "modality gap" that separates image and text
/// embeddings produced by CLIP-style models (e.g. <c>nomic-embed-vision-v1.5</c>
/// aligned to <c>nomic-embed-text-v1.5</c>).
///
/// Contrastive multimodal models project both modalities into a nominally
/// shared space, but the image and text clusters stay separated by a roughly
/// constant offset (the "gap"). The practical consequence is that raw cosine
/// similarities across modalities live on a much lower scale than within a
/// modality: image↔text cosines are typically ~0.01–0.10 while text↔text
/// cosines are ~0.4–0.9, so in a mixed collection text entries always dominate
/// the ranking. See Liang et al., "Mind the Gap" (2022).
///
/// The gap is estimated as the difference between the mean image embedding and
/// the mean text embedding of a reference set (typically the collection itself,
/// or a small labelled validation set). Correcting an image means subtracting
/// the gap and re-normalizing, which moves the image cluster onto the text
/// cluster and brings cross-modal similarities onto the within-modal scale.
///
/// Typical usage: at INDEX time, before storing image vectors — e.g. from the
/// .NET client before uploading the vectors of a unified collection.
/// </summary>
public sealed class ModalityGapCalibrator
{
  private readonly float[] _gap;
  private readonly int _dimensions;

  /// <summary>
  /// Estimates the modality gap from paired reference embeddings.
  /// </summary>
  /// <param name="imageEmbeddings">Reference image embeddings (e.g. of the collection images).</param>
  /// <param name="textEmbeddings">Reference text embeddings (e.g. of the collection documents).</param>
  public ModalityGapCalibrator(IReadOnlyList<float[]> imageEmbeddings, IReadOnlyList<float[]> textEmbeddings)
  {
    ArgumentNullException.ThrowIfNull(imageEmbeddings);
    ArgumentNullException.ThrowIfNull(textEmbeddings);

    if (imageEmbeddings.Count == 0 || textEmbeddings.Count == 0)
      throw new ArgumentException("At least one embedding per modality is required to estimate the gap.");

    _dimensions = imageEmbeddings[0].Length;
    if (imageEmbeddings.Any(e => e.Length != _dimensions) || textEmbeddings.Any(e => e.Length != _dimensions))
      throw new ArgumentException($"All embeddings must share the same dimensions ({_dimensions}).");

    var imageMean = new float[_dimensions];
    var textMean = new float[_dimensions];
    foreach (var embedding in imageEmbeddings)
      AddInPlace(imageMean, embedding);
    foreach (var embedding in textEmbeddings)
      AddInPlace(textMean, embedding);
    ScaleInPlace(imageMean, 1f / imageEmbeddings.Count);
    ScaleInPlace(textMean, 1f / textEmbeddings.Count);

    // gap = mean(image) - mean(text): subtracting it from an image moves it
    // onto the text cluster, adding it to a text moves it onto the image one.
    _gap = new float[_dimensions];
    for (var i = 0; i < _dimensions; i++)
      _gap[i] = imageMean[i] - textMean[i];
  }

  /// <summary>The estimated gap vector (mean image embedding minus mean text embedding).</summary>
  public ReadOnlySpan<float> Gap => _gap;

  /// <summary>
  /// Corrects an image embedding by subtracting the gap and re-normalizing,
  /// so it can be compared on the same scale as text embeddings.
  /// </summary>
  public float[] CorrectImage(float[] image) => Correct(image, gapSign: -1f);

  /// <summary>
  /// Corrects a text embedding by adding the gap and re-normalizing, so it can
  /// be compared on the same scale as image embeddings.
  /// </summary>
  public float[] CorrectText(float[] text) => Correct(text, gapSign: +1f);

  private float[] Correct(float[] vector, float gapSign)
  {
    ArgumentNullException.ThrowIfNull(vector);
    if (vector.Length != _dimensions)
      throw new ArgumentException($"Expected a {_dimensions}-dimensional vector, got {vector.Length}.");

    var corrected = new float[_dimensions];
    for (var i = 0; i < _dimensions; i++)
      corrected[i] = vector[i] + gapSign * _gap[i];

    NormalizeInPlace(corrected);
    return corrected;
  }

  private static void AddInPlace(float[] target, float[] source)
  {
    for (var i = 0; i < target.Length; i++)
      target[i] += source[i];
  }

  private static void ScaleInPlace(float[] vector, float factor)
  {
    for (var i = 0; i < vector.Length; i++)
      vector[i] *= factor;
  }

  private static void NormalizeInPlace(float[] vector)
  {
    var normSquared = 0d;
    foreach (var value in vector)
      normSquared += (double)value * value;

    if (normSquared <= 0d)
      return;

    var norm = Math.Sqrt(normSquared);
    for (var i = 0; i < vector.Length; i++)
      vector[i] = (float)(vector[i] / norm);
  }
}
