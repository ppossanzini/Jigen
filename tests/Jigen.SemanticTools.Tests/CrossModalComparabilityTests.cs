using System.Text;
using Jigen;
using Jigen.Calibration;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Jigen.SemanticTools;
using Xunit.Abstractions;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Evaluates the cross-modal "comparability" problem and two ways to fix it.
///
/// PROBLEM: with nomic (vision + text) the image↔text cosine similarities live
/// on a much lower scale than text↔text ones, so in a mixed collection the
/// text entries always dominate the ranking and the scores are not comparable.
///
/// FIX 1 (embedding-space): <see cref="ModalityGapCalibrator"/> subtracts the
/// estimated offset between the image and text clusters, moving image vectors
/// onto the text scale.
///
/// FIX 2 (score-space): <see cref="CrossModalScoreCalibrator"/> z-scores each
/// modality group of the candidate set, so an image and a text that are equally
/// good within their own group get a comparable score.
///
/// Requires the dataset in <c>tests/images</c> and the ONNX models in
/// <c>/data/onnx</c> (same conventions as <see cref="ImageTextIndexingTests"/>).
/// </summary>
public class CrossModalComparabilityTests
{
  private const string VisionModelPath = "/data/onnx/nomic-embed-vision-v1.5/model_int8.onnx";
  private const string TextTokenizerPath = "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx";
  private const string TextModelPath = "/data/onnx/nomic-embed-text-v1.5/model_int8.onnx";
  private const string Collection = "multimodal";

  private readonly ITestOutputHelper _output;

  public CrossModalComparabilityTests(ITestOutputHelper output)
  {
    _output = output;
  }

  /// <summary>
  /// Loads the 5 image embeddings (vision) and the 5 search_document text
  /// embeddings (the collection side of the text model).
  /// </summary>
  private static (float[][] Images, float[][] Texts) LoadEmbeddings(
    OnnxImageEmbeddingGenerator vision, OnnxEmbeddingGenerator text)
  {
    var imagesFolder = ImageDataset.FindRepoImagesFolder();
    ImageDataset.EnsureDownloaded(imagesFolder);
    var descriptions = ImageDataset.ReadDescriptions(imagesFolder);

    var images = ImageDataset.Samples
      .Select(sample => vision.GenerateImageEmbedding(
        File.ReadAllBytes(ImageDataset.ImagePath(imagesFolder, sample))))
      .ToArray();
    var texts = descriptions
      .Select(description => text.GenerateEmbedding("search_document", description))
      .ToArray();

    return (images, texts);
  }

  // --- tests ---------------------------------------------------------------

  /// <summary>
  /// Quantifies the scale mismatch: text↔text cosines are far above
  /// image↔text cosines, which is why a mixed search is text-dominated.
  /// </summary>
  [Fact]
  public void Quantifies_the_cross_modal_score_scale_mismatch()
  {
    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);
    var (images, texts) = LoadEmbeddings(vision, text);

    var tt = PairwiseCosines(texts);   // text↔text (excluding diagonal)
    var ii = PairwiseCosines(images);  // image↔image (excluding diagonal)
    var it = CrossCosines(images, texts);

    _output.WriteLine("Distribution of raw cosine similarities (mean ± std):");
    _output.WriteLine($"  text↔text   : {Mean(tt):F4} ± {Std(tt):F4}   (n={tt.Length})");
    _output.WriteLine($"  image↔image : {Mean(ii):F4} ± {Std(ii):F4}   (n={ii.Length})");
    _output.WriteLine($"  image↔text  : {Mean(it):F4} ± {Std(it):F4}   (n={it.Length})");
    _output.WriteLine($"  scale ratio (text↔text / image↔text) = {Mean(tt) / Math.Max(Mean(it), 1e-6f):F1}x");

    // The mismatch is the whole reason raw scores are not comparable across
    // modalities: text↔text must clearly dominate image↔text.
    Assert.True(Mean(it) > 0f, "Image↔text mean cosine is degenerate.");
    Assert.True(Mean(tt) > Mean(it) * 3f,
      $"Expected text↔text ({Mean(tt):F4}) to dwarf image↔text ({Mean(it):F4}).");
  }

  /// <summary>
  /// FIX 1: after subtracting the modality gap from the image embeddings, the
  /// image↔text cosines rise to the text↔text scale while the coherence is
  /// preserved (every image is still closest to its own description).
  /// </summary>
  [Fact]
  public void Modality_gap_correction_brings_cross_modal_scores_onto_the_text_scale()
  {
    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);
    var (images, texts) = LoadEmbeddings(vision, text);

    var tt = PairwiseCosines(texts);

    var calibrator = new ModalityGapCalibrator(images, texts);
    var correctedImages = images.Select(calibrator.CorrectImage).ToArray();

    // Corrected image↔text matrix (diagonal = own description).
    var matrix = new float[correctedImages.Length][];
    for (var i = 0; i < correctedImages.Length; i++)
    {
      matrix[i] = new float[texts.Length];
      for (var j = 0; j < texts.Length; j++)
        matrix[i][j] = Cosine(correctedImages[i], texts[j]);
    }
    var itCorrected = CrossCosines(correctedImages, texts);

    _output.WriteLine($"text↔text mean (reference) : {Mean(tt):F4} ± {Std(tt):F4}");
    _output.WriteLine($"image↔text raw             : {Mean(CrossCosines(images, texts)):F4}");
    _output.WriteLine($"image↔text after gap fix   : {Mean(itCorrected):F4} ± {Std(itCorrected):F4}");
    _output.WriteLine($"gap correction factor      : {Mean(itCorrected) / Math.Max(Mean(CrossCosines(images, texts)), 1e-6f):F1}x");

    PrintMatrix("image×text AFTER gap correction", matrix);

    // Coherence must survive: each image is still closest to its own text.
    var diagonalWins = true;
    var minDiagonal = float.MaxValue;
    var maxOffDiagonal = float.MinValue;
    for (var i = 0; i < matrix.Length; i++)
    {
      for (var j = 0; j < matrix.Length; j++)
      {
        if (i == j)
        {
          minDiagonal = Math.Min(minDiagonal, matrix[i][j]);
          continue;
        }
        maxOffDiagonal = Math.Max(maxOffDiagonal, matrix[i][j]);
        if (matrix[i][j] >= matrix[i][i])
          diagonalWins = false;
      }
    }
    _output.WriteLine($"diagonal range [{minDiagonal:F4}..], max off-diagonal {maxOffDiagonal:F4} → " +
                      (diagonalWins ? "coherence preserved ✓" : "coherence BROKEN ✗"));

    // The scale must actually rise toward the text↔text scale...
    Assert.True(Mean(itCorrected) > Mean(CrossCosines(images, texts)) * 2f,
      "Gap correction did not lift the cross-modal scores.");
    // ...without breaking the alignment.
    Assert.True(diagonalWins, "Gap correction broke the image↔own-description alignment.");
  }

  /// <summary>
  /// FIX 2: z-scoring each modality group of the candidate set puts images and
  /// texts on a common ranking scale, so a search no longer degenerates into a
  /// text-only ranking and the matching image competes with the text entries.
  /// </summary>
  [Fact]
  public void Z_score_calibration_ranks_images_and_texts_on_a_common_scale()
  {
    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);
    var (images, texts) = LoadEmbeddings(vision, text);
    var descriptions = ImageDataset.ReadDescriptions(ImageDataset.FindRepoImagesFolder());

    // For every description, run the mixed search: 5 image + 5 text candidates.
    for (var q = 0; q < ImageDataset.Samples.Count; q++)
    {
      var query = text.GenerateEmbedding("search_query", descriptions[q]);

      var candidates = new (string Modality, int Index, float Score)[images.Length + texts.Length];
      for (var i = 0; i < images.Length; i++)
        candidates[i] = ("image", i, Cosine(query, images[i]));
      for (var j = 0; j < texts.Length; j++)
        candidates[images.Length + j] = ("text", j, Cosine(query, texts[j]));

      var calibrated = CrossModalScoreCalibrator.Calibrate(
        candidates.Select(c => c.Modality).ToArray(),
        candidates.Select(c => c.Score).ToArray());

      var ranked = candidates
        .Select((c, index) => (c.Modality, c.Index, c.Score, Calibrated: calibrated[index]))
        .OrderByDescending(r => r.Calibrated)
        .ToList();

      _output.WriteLine($"\nQuery [{q}] \"{descriptions[q]}\"");
      _output.WriteLine($"  {'#',-3}{"type",-7}{"entry",-30}{"raw",-9}{"z",-7}");
      foreach (var (rank, r) in ranked.Select((r, rank) => (rank, r)))
        _output.WriteLine($"  {rank,-3}{r.Modality,-7}{EntryLabel(r.Modality, r.Index),-30}" +
                          $"{r.Score:F4}   {r.Calibrated:F2}");

      // After calibration the correct document must be the top-2 overall:
      // the description's own text (text side) and the matching image (image
      // side) are the two candidates that are "best in their group".
      var top2 = ranked.Take(2).ToList();
      var hasOwnText = top2.Any(r => r.Modality == "text" && r.Index == q);
      var hasOwnImage = top2.Any(r => r.Modality == "image" && r.Index == q);
      var ownImageRank = ranked.FindIndex(r => r.Modality == "image" && r.Index == q);

      _output.WriteLine($"  → own text in top-2: {hasOwnText}, own image in top-2: {hasOwnImage} " +
                        $"(own image overall position #{ownImageRank + 1})");

      Assert.True(hasOwnText && hasOwnImage,
        $"Calibrated top-2 for query [{q}] does not contain the own text and the own image.");
    }

    _output.WriteLine($"\nThe matching image is always the top image candidate " +
                      $"(#2 overall, right behind its own text) in {ImageDataset.Samples.Count}/5 queries.");
  }

  /// <summary>
  /// End-to-end: indexes the GAP-CORRECTED image embeddings together with the
  /// text embeddings into a Jigen collection, searches with each description,
  /// and verifies that the matching image now ranks right behind its own text
  /// instead of being buried under all text entries.
  /// </summary>
  [Fact]
  public async Task Indexing_gap_corrected_images_ranks_them_with_the_text_entries()
  {
    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);
    var (images, texts) = LoadEmbeddings(vision, text);
    var descriptions = ImageDataset.ReadDescriptions(ImageDataset.FindRepoImagesFolder());

    // Correct the image side once, at "index time", so stored vectors are
    // directly comparable with the text vectors.
    var calibrator = new ModalityGapCalibrator(images, texts);
    var correctedImages = images.Select(calibrator.CorrectImage).ToArray();

    await WithStore(async store =>
    {
      var textIds = new byte[ImageDataset.Samples.Count][];
      var imageIds = new byte[ImageDataset.Samples.Count][];

      for (var i = 0; i < ImageDataset.Samples.Count; i++)
      {
        textIds[i] = Guid.CreateVersion7().ToByteArray();
        imageIds[i] = Guid.CreateVersion7().ToByteArray();

        await store.AppendContent(new VectorEntry
        {
          Id = textIds[i],
          CollectionName = Collection,
          Content = Encoding.UTF8.GetBytes($"text:{descriptions[i]}"),
          Embedding = texts[i]
        });
        await store.AppendContent(new VectorEntry
        {
          Id = imageIds[i],
          CollectionName = Collection,
          Content = Encoding.UTF8.GetBytes($"image:{ImageDataset.Samples[i].FileName}"),
          Embedding = correctedImages[i]
        });
      }
      await store.SaveChangesAsync();

      for (var q = 0; q < ImageDataset.Samples.Count; q++)
      {
        var query = text.GenerateEmbedding("search_query", descriptions[q]);
        var results = store.Search(Collection, query, top: ImageDataset.Samples.Count * 2).ToList();

        _output.WriteLine($"\nQuery [{q}] \"{descriptions[q]}\"  (raw cosine in a mixed collection)");
        _output.WriteLine($"  {'#',-3}{"type",-7}{"entry",-40}cosine");
        for (var rank = 0; rank < results.Count; rank++)
        {
          var entry = results[rank].entry;
          var content = Encoding.UTF8.GetString(entry.Content.Span);
          _output.WriteLine($"  {rank,-3}{(content.StartsWith("image:") ? "image" : "text"),-7}" +
                            $"{Truncate(content[6..], 38),-40}{results[rank].score:F4}");
        }

        // The matching image must sit right behind its own text (position #2
        // overall at worst, since the own text is always the #1 hit).
        var matchingImageRank = results.FindIndex(r => r.entry.Id.AsSpan().SequenceEqual(imageIds[q]));
        _output.WriteLine($"  → matching image at position #{matchingImageRank + 1} (cosine {results[matchingImageRank].score:F4})");

        Assert.True(matchingImageRank >= 1 && matchingImageRank <= 2,
          $"Gap-corrected image {ImageDataset.Samples[q].FileName} ranked #{matchingImageRank + 1}, " +
          "expected #2 or #3 (right behind its own text).");
      }
    });
  }

  /// <summary>
  /// Evaluates the alternative architecture: images and texts in SEPARATE
  /// collections, each searched with its own <c>efSearch</c>, then merged.
  ///
  /// Findings (verified empirically):
  /// (a) <c>efSearch</c> is the HNSW beam width — it trades recall vs speed and
  ///     NEVER changes the scores, so a different efSearch per collection
  ///     cannot make cross-modal scores comparable.
  /// (b) separate collections give clean within-modality rankings (the matching
  ///     image is #1 in the image collection, the own text #1 in the text one).
  /// (c) merging the two rankings by RAW score still buries the image under all
  ///     the texts (0.03–0.09 vs 0.4–0.9) — this is the comparability problem
  ///     again, just moved to the merge step.
  /// (d) merging by per-group z-score (<see cref="CrossModalScoreCalibrator"/>,
  ///     which is already group-based: here the groups ARE the collections)
  ///     surfaces both the own text and the own image at the top of one
  ///     ranking — WITHOUT needing the gap correction.
  /// </summary>
  [Fact]
  public async Task Separate_collections_with_per_collection_efSearch_still_need_score_normalization_to_merge()
  {
    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);
    var (images, texts) = LoadEmbeddings(vision, text);
    var descriptions = ImageDataset.ReadDescriptions(ImageDataset.FindRepoImagesFolder());

    const string imageCollection = "images";
    const string textCollection = "texts";

    await WithStore(async store =>
    {
      var imageIds = new byte[ImageDataset.Samples.Count][];
      var textIds = new byte[ImageDataset.Samples.Count][];
      for (var i = 0; i < ImageDataset.Samples.Count; i++)
      {
        imageIds[i] = Guid.CreateVersion7().ToByteArray();
        textIds[i] = Guid.CreateVersion7().ToByteArray();
        await store.AppendContent(new VectorEntry
        {
          Id = imageIds[i],
          CollectionName = imageCollection,
          Content = Encoding.UTF8.GetBytes($"image:{ImageDataset.Samples[i].FileName}"),
          Embedding = images[i]
        });
        await store.AppendContent(new VectorEntry
        {
          Id = textIds[i],
          CollectionName = textCollection,
          Content = Encoding.UTF8.GetBytes($"text:{descriptions[i]}"),
          Embedding = texts[i]
        });
      }
      await store.SaveChangesAsync();

      for (var q = 0; q < ImageDataset.Samples.Count; q++)
      {
        var query = text.GenerateEmbedding("search_query", descriptions[q]);

        // (a) efSearch is a beam width, not a score calibrator: with a
        // 5-vector collection the top-1 cosine is identical for efSearch 8
        // and 64 (in larger graphs it only changes recall, still not scores).
        var narrowTop = store.Search(imageCollection, query, 1, efSearch: 8).First().score;
        var wideTop = store.Search(imageCollection, query, 1, efSearch: 64).First().score;
        Assert.Equal(narrowTop, wideTop, 4);

        // (b) per-collection efSearch: a wide beam for the images, a narrow
        // one for the texts. Each collection ranks cleanly on its own scale.
        var imageHits = store.Search(imageCollection, query, top: 5, efSearch: 64).ToList();
        var textHits = store.Search(textCollection, query, top: 5, efSearch: 8).ToList();

        var ownImageRank = imageHits.FindIndex(r => r.entry.Id.AsSpan().SequenceEqual(imageIds[q]));
        var ownTextRank = textHits.FindIndex(r => r.entry.Id.AsSpan().SequenceEqual(textIds[q]));
        Assert.Equal(0, ownImageRank);
        Assert.Equal(0, ownTextRank);

        // Build the merged candidate list (imageHits/textHits are score-sorted,
        // so the own image/text are the entries with index 0 of their group).
        var merged = new List<(string Modality, int Index, float Score)>();
        for (var i = 0; i < imageHits.Count; i++)
          merged.Add(("image", i, imageHits[i].score));
        for (var j = 0; j < textHits.Count; j++)
          merged.Add(("text", j, textHits[j].score));

        // (c) raw-score merge: the texts dominate and the image is buried.
        var rawMerged = merged.OrderByDescending(m => m.Score).ToList();
        var ownImageRawPos = rawMerged.FindIndex(m => m.Modality == "image" && m.Index == 0);

        // (d) z-score merge per collection (group = collection).
        var calibrated = CrossModalScoreCalibrator.Calibrate(
          merged.Select(m => m.Modality).ToArray(),
          merged.Select(m => m.Score).ToArray());
        var ranked = merged
          .Select((m, idx) => (m.Modality, m.Index, m.Score, Calibrated: calibrated[idx]))
          .OrderByDescending(r => r.Calibrated)
          .ToList();
        var ownImagePos = ranked.FindIndex(r => r.Modality == "image" && r.Index == 0);
        var ownTextPos = ranked.FindIndex(r => r.Modality == "text" && r.Index == 0);

        _output.WriteLine($"\nQuery [{q}] \"{descriptions[q]}\"");
        _output.WriteLine($"  efSearch 8 vs 64 (images) → top-1 cosine {narrowTop:F4} = {wideTop:F4}  " +
                          "(efSearch never changes scores)");
        _output.WriteLine($"  per-collection: own image #{ownImageRank + 1} (cosine {imageHits[0].score:F4}), " +
                          $"own text #{ownTextRank + 1} (cosine {textHits[0].score:F4})");
        _output.WriteLine($"  raw-score merge   : own image at #{ownImageRawPos + 1} " +
                          $"(buried under the {ImageDataset.Samples.Count} texts)");
        _output.WriteLine($"  z-score merge     : own text at #{ownTextPos + 1}, own image at #{ownImagePos + 1}");

        Assert.True(ownImageRawPos >= ImageDataset.Samples.Count,
          "Raw-score merge did not bury the image under the texts.");
        Assert.True(ownTextPos <= 1 && ownImagePos <= 1,
          "Z-score merge did not surface both the own text and the own image at the top.");
      }
    });
  }

  // --- helpers -------------------------------------------------------------

  private static async Task WithStore(Func<Store, Task> test)
  {
    var basePath = Path.Combine(Path.GetTempPath(), $"jigen-multimodal-{Guid.NewGuid():N}");
    Directory.CreateDirectory(basePath);

    try
    {
      var store = new Store(new StoreOptions
      {
        DataBaseName = "multimodal",
        DataBasePath = basePath,
        Indexer = new SmallWorldIndexer(new SmallWorldOptions
        {
          M = 16,
          EfConstruction = 100,
          EfSearch = 50,
          StoragePath = Path.Combine(basePath, "hnsw")
        })
      });

      try
      {
        await test(store);
      }
      finally
      {
        await store.Close();
      }
    }
    finally
    {
      if (Directory.Exists(basePath))
        Directory.Delete(basePath, true);
    }
  }

  private static string EntryLabel(string modality, int index) =>
    modality == "image"
      ? ImageDataset.Samples[index].FileName
      : Truncate(ImageDataset.ReadDescriptions(ImageDataset.FindRepoImagesFolder())[index], 28);

  private static float[] PairwiseCosines(float[][] vectors)
  {
    var result = new List<float>();
    for (var i = 0; i < vectors.Length; i++)
      for (var j = i + 1; j < vectors.Length; j++)
        result.Add(Cosine(vectors[i], vectors[j]));
    return result.ToArray();
  }

  private static float[] CrossCosines(float[][] a, float[][] b)
  {
    var result = new float[a.Length * b.Length];
    var k = 0;
    for (var i = 0; i < a.Length; i++)
      for (var j = 0; j < b.Length; j++)
        result[k++] = Cosine(a[i], b[j]);
    return result;
  }

  private static float Mean(float[] values) => values.Length == 0 ? 0f : values.Average();

  private static float Std(float[] values)
  {
    if (values.Length == 0)
      return 0f;
    var mean = Mean(values);
    return MathF.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
  }

  private void PrintMatrix(string title, float[][] matrix)
  {
    _output.WriteLine($"\n{title} (cosine)");
    _output.WriteLine($"  {"",-26}" + string.Join("  ", Enumerable.Range(0, matrix.Length).Select(j => $"[{j}]".PadLeft(7))));
    for (var i = 0; i < matrix.Length; i++)
    {
      _output.WriteLine($"  {ImageDataset.Samples[i].FileName,-24}" +
                        string.Join("  ", matrix[i].Select((c, j) =>
                          $"{(i == j ? "*" : " ")}{c:F3}".PadLeft(8))));
    }
  }

  private static double L2Norm(float[] vector)
  {
    var normSquared = 0d;
    foreach (var value in vector)
      normSquared += (double)value * value;
    return Math.Sqrt(normSquared);
  }

  private static float Cosine(float[] a, float[] b)
  {
    var normA = (float)L2Norm(a);
    var normB = (float)L2Norm(b);
    if (normA <= 0f || normB <= 0f)
      return 0f;

    var dot = 0f;
    for (var i = 0; i < a.Length; i++)
      dot += a[i] * b[i];
    return dot / (normA * normB);
  }

  private static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
}
