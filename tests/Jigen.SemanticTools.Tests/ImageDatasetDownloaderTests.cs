using System.Net.Http.Headers;
using System.Text.Json;
using SixLabors.ImageSharp;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// Builds the small multimodal test dataset: downloads the five sample images
/// (Wikimedia Commons, 800px) into the repository's <c>tests/images</c> folder
/// and writes a <c>descriptions.json</c> next to them mapping each relative
/// path (the file name) to its description.
///
/// Idempotent: files that already exist on disk are not re-downloaded, so the
/// test only touches the network for missing images. Requires internet access
/// on first run.
/// </summary>
public class ImageDatasetDownloaderTests
{
  [Fact]
  public async Task Download_five_images_and_write_descriptions_json()
  {
    var imagesFolder = ImageDataset.FindRepoImagesFolder();

    using var http = new HttpClient(new HttpClientHandler
    {
      AllowAutoRedirect = true, // Special:FilePath 302 → the actual upload file
      MaxAutomaticRedirections = 5
    })
    {
      Timeout = TimeSpan.FromSeconds(60)
    };
    // Wikimedia requires a descriptive User-Agent (its default policy blocks
    // bare HttpClient/curl user agents).
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JigenSemanticToolsTests", "1.0"));

    var descriptions = new Dictionary<string, string>();

    foreach (var sample in ImageDataset.Samples)
    {
      var targetPath = ImageDataset.ImagePath(imagesFolder, sample);

      if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
      {
        var bytes = await http.GetByteArrayAsync(sample.SourceUrl);

        // The file must be a decodable image, or the vision model and the
        // indexing tests downstream would fail on a corrupted download.
        using var _ = Image.Load(bytes);

        await File.WriteAllBytesAsync(targetPath, bytes);
      }

      Assert.True(File.Exists(targetPath) && new FileInfo(targetPath).Length > 0,
        $"Image not available after download: {targetPath}");

      descriptions[sample.FileName] = sample.Description;
    }

    // The JSON lives in the same folder and uses the path relative to it, i.e.
    // the file name of each image.
    var jsonPath = ImageDataset.DescriptionsJsonPath(imagesFolder);
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(
      descriptions,
      new JsonSerializerOptions { WriteIndented = true }));

    // Round-trip check: the file must parse back into the expected dataset.
    var written = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(jsonPath));
    Assert.NotNull(written);
    Assert.Equal(ImageDataset.Samples.Count, written.Count);
    foreach (var sample in ImageDataset.Samples)
      Assert.Equal(sample.Description, written[sample.FileName]);
  }
}
