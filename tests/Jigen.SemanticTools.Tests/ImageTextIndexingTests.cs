using System.Text;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Indexer;
using Xunit.Abstractions;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Indexes the five downloaded images and their text descriptions into the
/// SAME Jigen collection using the real nomic models in <c>/data/onnx</c>:
/// <list type="bullet">
/// <item>images → <c>nomic-embed-vision-v1.5</c> (model_int8.onnx)</item>
/// <item>text  → <c>nomic-embed-text-v1.5</c> (tokenizer.onnx + model_int8.onnx)</item>
/// </list>
/// Both models share one embedding space, so image vectors and text vectors can
/// live side by side in the collection and be compared/searched together.
///
/// Requires the dataset in <c>tests/images</c> (see <see cref="ImageDatasetDownloaderTests"/>)
/// and the ONNX models under <c>/data/onnx</c> (same convention as JigenStoreTests).
/// </summary>
public class ImageTextIndexingTests
{
  private const string VisionModelPath = "/data/onnx/nomic-embed-vision-v1.5/model_int8.onnx";
  private const string TextTokenizerPath = "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx";
  private const string TextModelPath = "/data/onnx/nomic-embed-text-v1.5/model_int8.onnx";

  private const string Collection = "multimodal";
  private const string ImagePrefix = "image:";
  private const string TextPrefix = "text:";

  private readonly ITestOutputHelper _output;

  public ImageTextIndexingTests(ITestOutputHelper output)
  {
    _output = output;
  }

  /// <summary>
  /// Builds the shared collection: one vector per image (vision) and one vector
  /// per description (text, search_document prefix) in the same collection.
  /// Returns the sample ids, image embeddings and text embeddings so the
  /// search/cosine tests can reference them.
  /// </summary>
  private static async Task<IndexedData> EmbedAndIndexAsync(
    Store store, string imagesFolder, IReadOnlyList<string> descriptions,
    OnnxImageEmbeddingGenerator vision, OnnxEmbeddingGenerator text)
  {
    var imageIds = new byte[ImageDataset.Samples.Count][];
    var textIds = new byte[ImageDataset.Samples.Count][];
    var imageEmbeddings = new float[ImageDataset.Samples.Count][];
    var textEmbeddings = new float[ImageDataset.Samples.Count][];

    for (var i = 0; i < ImageDataset.Samples.Count; i++)
    {
      var sample = ImageDataset.Samples[i];

      var imageEmbedding = vision.GenerateImageEmbedding(
        File.ReadAllBytes(ImageDataset.ImagePath(imagesFolder, sample)));
      var textEmbedding = text.GenerateEmbedding("search_document", descriptions[i]);

      imageIds[i] = Guid.CreateVersion7().ToByteArray();
      textIds[i] = Guid.CreateVersion7().ToByteArray();
      imageEmbeddings[i] = imageEmbedding;
      textEmbeddings[i] = textEmbedding;

      // Same collection: image vector and its text vector side by side.
      await store.AppendContent(new VectorEntry
      {
        Id = imageIds[i],
        CollectionName = Collection,
        Content = Encoding.UTF8.GetBytes($"{ImagePrefix}{sample.FileName}"),
        Embedding = imageEmbedding
      });
      await store.AppendContent(new VectorEntry
      {
        Id = textIds[i],
        CollectionName = Collection,
        Content = Encoding.UTF8.GetBytes($"{TextPrefix}{descriptions[i]}"),
        Embedding = textEmbedding
      });
    }

    await store.SaveChangesAsync();
    return new IndexedData(imageIds, textIds, imageEmbeddings, textEmbeddings);
  }

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

  private sealed record IndexedData(
    byte[][] ImageIds, byte[][] TextIds, float[][] ImageEmbeddings, float[][] TextEmbeddings);

  // --- tests ---------------------------------------------------------------

  /// <summary>
  /// Indexes 5 image vectors and 5 text vectors into the same collection and
  /// verifies the store actually persisted both kinds of entries, all unit-norm.
  /// </summary>
  [Fact]
  public async Task Index_images_and_text_descriptions_in_the_same_collection()
  {
    var imagesFolder = ImageDataset.FindRepoImagesFolder();
    ImageDataset.EnsureDownloaded(imagesFolder);
    var descriptions = ImageDataset.ReadDescriptions(imagesFolder);

    Assert.True(File.Exists(VisionModelPath), $"Missing vision model: {VisionModelPath}");
    Assert.True(File.Exists(TextTokenizerPath) && File.Exists(TextModelPath),
      $"Missing text model: {TextTokenizerPath} / {TextModelPath}");

    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);

    await WithStore(async store =>
    {
      var data = await EmbedAndIndexAsync(store, imagesFolder, descriptions, vision, text);

      var allEntries = store.Search(Collection).ToList();
      Assert.Equal(ImageDataset.Samples.Count * 2, allEntries.Count);

      var images = allEntries.Where(e => ContentIs(e, ImagePrefix)).ToList();
      var texts = allEntries.Where(e => ContentIs(e, TextPrefix)).ToList();
      Assert.Equal(ImageDataset.Samples.Count, images.Count);
      Assert.Equal(ImageDataset.Samples.Count, texts.Count);

      // Every stored vector must be a 768-dim unit vector (dot product = cosine).
      Assert.All(data.ImageEmbeddings, e => AssertUnitVector(e));
      Assert.All(data.TextEmbeddings, e => AssertUnitVector(e));

      _output.WriteLine($"Indexed {allEntries.Count} entries in collection '{Collection}' " +
                        $"({images.Count} images + {texts.Count} texts), all 768-dim unit vectors.");
      _output.WriteLine($"Collection on disk: {store.GetCollections().FirstOrDefault(c => c == Collection)}");
    });
  }

  /// <summary>
  /// Searches with each description as a query (search_query prefix) and checks
  /// that the matching image is the top-ranked image in the mixed collection
  /// (the text entry of the same description usually outranks it, which is
  /// expected). Prints the full ranking with cosine similarity.
  /// </summary>
  [Fact]
  public async Task Search_by_description_returns_the_matching_image_first()
  {
    var imagesFolder = ImageDataset.FindRepoImagesFolder();
    ImageDataset.EnsureDownloaded(imagesFolder);
    var descriptions = ImageDataset.ReadDescriptions(imagesFolder);

    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);

    await WithStore(async store =>
    {
      var data = await EmbedAndIndexAsync(store, imagesFolder, descriptions, vision, text);

      for (var i = 0; i < ImageDataset.Samples.Count; i++)
      {
        var query = text.GenerateEmbedding("search_query", descriptions[i]);
        var results = store.Search(Collection, query, top: ImageDataset.Samples.Count * 2).ToList();

        _output.WriteLine($"\nQuery [{i}] \"{descriptions[i]}\"");
        _output.WriteLine($"  {'#',-3}{"type",-7}{"content",-52}cosine   cosineDist");
        for (var rank = 0; rank < results.Count; rank++)
        {
          var entry = results[rank].entry;
          var content = Encoding.UTF8.GetString(entry.Content.Span);
          var type = ContentIs(entry, ImagePrefix) ? "image" : "text";
          _output.WriteLine($"  {rank,-3}{type,-7}{Truncate(content, 50),-52}" +
                            $"{results[rank].score:F4}  {1f - results[rank].score:F4}");
        }

        // The matching image must be the first image in the ranking (the text
        // entry of the same description usually outranks everything, which is
        // expected in a mixed collection — text↔text similarity is far higher
        // than image↔text).
        var matchingImageRank = results.FindIndex(r => r.entry.Id.AsSpan().SequenceEqual(data.ImageIds[i]));
        var firstImageRank = results.FindIndex(r => ContentIs(r.entry, ImagePrefix));

        Assert.True(matchingImageRank >= 0, $"Image {ImageDataset.Samples[i].FileName} not found in results.");
        Assert.Equal(firstImageRank, matchingImageRank);

        // Cross-modal margin: the matching image must beat every other image.
        var matchingScore = results[matchingImageRank].score;
        var bestOtherImageScore = results
          .Where(r => ContentIs(r.entry, ImagePrefix) && !r.entry.Id.AsSpan().SequenceEqual(data.ImageIds[i]))
          .Select(r => r.score)
          .DefaultIfEmpty(-1f)
          .Max();
        var margin = matchingScore - bestOtherImageScore;

        _output.WriteLine($"  → matching image at rank {matchingImageRank} (cosine {matchingScore:F4}, " +
                          $"distance {1f - matchingScore:F4}), margin over best other image = {margin:F4}");

        Assert.True(margin > 0.005f,
          $"Matching image {ImageDataset.Samples[i].FileName} does not beat the other images " +
          $"(margin {margin:F4}).");
      }
    });
  }

  /// <summary>
  /// Evaluates the cosine distance between each image embedding and each
  /// description embedding (5×5 matrix). For the query-side alignment
  /// (search_query, the side the vision model is trained against) every image
  /// must be closest to its own description: the diagonal must be the max of
  /// its row. Also prints the document-side matrix for comparison.
  /// </summary>
  [Fact]
  public void Cosine_distance_between_images_and_descriptions()
  {
    var imagesFolder = ImageDataset.FindRepoImagesFolder();
    ImageDataset.EnsureDownloaded(imagesFolder);
    var descriptions = ImageDataset.ReadDescriptions(imagesFolder);

    using var vision = new OnnxImageEmbeddingGenerator(VisionModelPath);
    using var text = new OnnxEmbeddingGenerator(TextTokenizerPath, TextModelPath);

    var imageEmbeddings = ImageDataset.Samples
      .Select(sample => vision.GenerateImageEmbedding(File.ReadAllBytes(ImageDataset.ImagePath(imagesFolder, sample))))
      .ToArray();

    // Query-side and document-side text embeddings.
    var queryTextEmbeddings = descriptions.Select(d => text.GenerateEmbedding("search_query", d)).ToArray();
    var documentTextEmbeddings = descriptions.Select(d => text.GenerateEmbedding("search_document", d)).ToArray();

    var queryMatrix = CosineMatrix(imageEmbeddings, queryTextEmbeddings);
    var documentMatrix = CosineMatrix(imageEmbeddings, documentTextEmbeddings);

    PrintMatrix("image × text (search_query)", queryMatrix, descriptions);
    PrintMatrix("image × text (search_document)", documentMatrix, descriptions);

    // Coherence gate on the aligned (query) side: each image's own description
    // is the closest text of all five.
    var maxOffDiagonal = 0f;
    for (var i = 0; i < ImageDataset.Samples.Count; i++)
    {
      for (var j = 0; j < ImageDataset.Samples.Count; j++)
      {
        if (i == j) continue;
        Assert.True(queryMatrix[i][j] < queryMatrix[i][i],
          $"Image [{i}] ({ImageDataset.Samples[i].FileName}) is closer to text [{j}] " +
          $"({descriptions[j]}) than to its own description: " +
          $"{queryMatrix[i][j]:F4} >= {queryMatrix[i][i]:F4}.");
        maxOffDiagonal = Math.Max(maxOffDiagonal, queryMatrix[i][j]);
      }
    }

    var meanDiagonal = Enumerable.Range(0, ImageDataset.Samples.Count).Average(i => queryMatrix[i][i]);
    var meanOffDiagonal = Enumerable.Range(0, ImageDataset.Samples.Count)
      .SelectMany(i => Enumerable.Range(0, ImageDataset.Samples.Count).Where(j => i != j).Select(j => queryMatrix[i][j]))
      .Average();

    _output.WriteLine($"\nquery-side   : mean diag cosine = {meanDiagonal:F4} (distance {1f - meanDiagonal:F4}), " +
                      $"mean off-diag = {meanOffDiagonal:F4}, margin = {meanDiagonal - meanOffDiagonal:F4}, " +
                      $"worst gap = {queryMatrix.Min(row => row.Max()) - maxOffDiagonal:F4}");
    _output.WriteLine("=> cosine distance image↔own description is well below the cross distances.");

    Assert.True(meanDiagonal > meanOffDiagonal,
      $"Mean diagonal cosine {meanDiagonal:F4} not above mean off-diagonal {meanOffDiagonal:F4}.");
  }

  // --- helpers -------------------------------------------------------------

  private static bool ContentIs(VectorEntry entry, string prefix) =>
    Encoding.UTF8.GetString(entry.Content.Span).StartsWith(prefix, StringComparison.Ordinal);

  private static void AssertUnitVector(float[] vector)
  {
    Assert.Equal(768, vector.Length);
    Assert.Equal(1d, L2Norm(vector), 3);
  }

  private static float[][] CosineMatrix(float[][] images, float[][] texts)
  {
    var matrix = new float[images.Length][];
    for (var i = 0; i < images.Length; i++)
    {
      matrix[i] = new float[texts.Length];
      for (var j = 0; j < texts.Length; j++)
        matrix[i][j] = Cosine(images[i], texts[j]);
    }
    return matrix;
  }

  private void PrintMatrix(string title, float[][] matrix, IReadOnlyList<string> descriptions)
  {
    _output.WriteLine($"\n{title}  (cosine similarity / cosine distance)");
    _output.WriteLine($"  {"",-6}" + string.Join("  ", descriptions.Select((d, j) => $"[{j}]".PadLeft(7))));
    for (var i = 0; i < matrix.Length; i++)
    {
      _output.WriteLine($"  {ImageDataset.Samples[i].FileName,-24}" +
                        string.Join("  ", matrix[i].Select((c, j) =>
                          $"{(i == j ? "*" : " ")}{c:F3}/{(1f - c):F3}".PadLeft(9))));
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
