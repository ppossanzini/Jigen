using System.Text.Json;

namespace Jigen.SemanticTools.Tests;

/// <summary>
/// A curated multimodal dataset used by the cross-modal tests: five real
/// photographs with a clear, unambiguous description each. The images are
/// downloaded from Wikimedia Commons (stable <c>Special:FilePath</c> URLs,
/// resized to 800px) into the repository's <c>tests/images</c> folder, and a
/// <c>descriptions.json</c> file in the same folder maps each relative path
/// (the file name) to its description.
/// </summary>
public sealed record ImageSample(string FileName, string Description, string SourceUrl);

public static class ImageDataset
{
  /// <summary>Name of the JSON file written next to the images.</summary>
  public const string DescriptionsJsonName = "descriptions.json";

  /// <summary>
  /// The five samples. Descriptions are in English because the nomic text
  /// model (and the vision/text cross-modal alignment) is trained on English;
  /// they must be unambiguous so a search clearly discriminates the images.
  /// </summary>
  public static readonly IReadOnlyList<ImageSample> Samples =
  [
    new(
      "cat-tabby.jpg",
      "a tabby cat sitting and looking at the camera",
      "https://commons.wikimedia.org/wiki/Special:FilePath/Cat_November_2010-1a.jpg?width=800"),
    new(
      "beach-bali.jpg",
      "a tropical beach in Bali with palm trees and turquoise sea",
      "https://commons.wikimedia.org/wiki/Special:FilePath/Bali_beach.jpg?width=800"),
    new(
      "ferrari-red-car.jpg",
      "a red Ferrari sports car parked on the street",
      "https://commons.wikimedia.org/wiki/Special:FilePath/Ferrari_458_Italia.jpg?width=800"),
    new(
      "spaghetti-carbonara.jpg",
      "a plate of spaghetti alla carbonara with egg and guanciale",
      "https://commons.wikimedia.org/wiki/Special:FilePath/Spaghetti_alla_carbonara.jpg?width=800"),
    new(
      "golden-retriever-dog.jpg",
      "a golden retriever dog",
      "https://commons.wikimedia.org/wiki/Special:FilePath/Golden_Retriever.jpg?width=800")
  ];

  /// <summary>
  /// Locates the repository's <c>tests/images</c> folder by walking up from the
  /// test output directory (bin/Debug/net10.0/...). Works both locally and in
  /// CI as long as the checkout contains the folder.
  /// </summary>
  public static string FindRepoImagesFolder()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
      var candidate = Path.Combine(current.FullName, "tests", "images");
      if (Directory.Exists(candidate))
        return candidate;
      current = current.Parent;
    }

    throw new DirectoryNotFoundException(
      $"Could not locate the repository 'tests/images' folder starting from '{AppContext.BaseDirectory}'.");
  }

  /// <summary>Full path of a sample image inside the images folder.</summary>
  public static string ImagePath(string imagesFolder, ImageSample sample) =>
    Path.Combine(imagesFolder, sample.FileName);

  /// <summary>Full path of the descriptions JSON inside the images folder.</summary>
  public static string DescriptionsJsonPath(string imagesFolder) =>
    Path.Combine(imagesFolder, DescriptionsJsonName);

  /// <summary>
  /// Reads <c>descriptions.json</c> (relative path → description) and returns
  /// the descriptions for the dataset samples, in dataset order.
  /// </summary>
  public static IReadOnlyList<string> ReadDescriptions(string imagesFolder)
  {
    var jsonPath = DescriptionsJsonPath(imagesFolder);
    if (!File.Exists(jsonPath))
      throw new FileNotFoundException(
        $"Missing '{DescriptionsJsonName}'. Run the ImageDatasetDownloaderTests test " +
        "(or download the images manually) before running the indexing tests.", jsonPath);

    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(jsonPath))
      ?? throw new InvalidDataException($"'{jsonPath}' is not a valid object of path → description.");

    return Samples.Select(sample =>
      map.TryGetValue(sample.FileName, out var description)
        ? description
        : throw new InvalidDataException($"'{jsonPath}' has no entry for '{sample.FileName}'."))
      .ToArray();
  }

  /// <summary>
  /// Asserts that every dataset image exists on disk and is decodable; throws a
  /// helpful message when the dataset has not been downloaded yet.
  /// </summary>
  public static void EnsureDownloaded(string imagesFolder)
  {
    var missing = Samples.Where(sample => !File.Exists(ImagePath(imagesFolder, sample))).ToList();
    if (missing.Count > 0)
      throw new FileNotFoundException(
        "The multimodal dataset is incomplete. Run the ImageDatasetDownloaderTests " +
        $"(downloads the images into '{imagesFolder}') before running the indexing tests. Missing: " +
        string.Join(", ", missing.Select(sample => sample.FileName)));
  }
}
