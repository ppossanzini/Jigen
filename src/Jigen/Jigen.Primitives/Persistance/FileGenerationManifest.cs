namespace Jigen.Persistance;

internal sealed record FileGenerationManifest(
  int Version, Guid Generation, Dictionary<string, string> Checksums, string[] ObsoleteFiles);