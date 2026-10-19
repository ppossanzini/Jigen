namespace Jigen.Persistance;

public sealed class FileGenerationOptions
{
  public required string DirectoryPath { get; init; }
  public string ManifestName { get; init; } = "generation";
}