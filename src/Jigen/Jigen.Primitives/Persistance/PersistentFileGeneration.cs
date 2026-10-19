using System.Security.Cryptography;
using System.Text.Json;

namespace Jigen.Persistance;

/// <summary>
/// Publishes a verified, closed file generation through one manifest replacement.
/// Pending source cleanup is resumed on opening after active files are verified.
/// The directory is exclusively owned; active files must not be changed until
/// cleanup completes. File flushes and rename do not guarantee power-loss recovery.
/// </summary>
public sealed class PersistentFileGeneration : IDisposable
{
  private readonly string _directory;
  private readonly string _manifestPath;
  private readonly string _manifestName;
  private readonly FileStream _ownership;
  private readonly Lock _gate = new();
  private FileGenerationManifest _active;
  private bool _disposed;
  private bool _failed;

  public static bool HasActiveGeneration(FileGenerationOptions options) =>
    File.Exists(Path.Combine(Path.GetFullPath(options.DirectoryPath), options.ManifestName + ".json"));

  public PersistentFileGeneration(FileGenerationOptions options)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(options.DirectoryPath);
    if (string.IsNullOrWhiteSpace(options.ManifestName) || options.ManifestName is "." or ".."
      || options.ManifestName.Contains('/') || options.ManifestName.Contains('\\'))
      throw new ArgumentException("A manifest base name is required.", nameof(options.ManifestName));
    _manifestName = options.ManifestName;
    _directory = Path.GetFullPath(options.DirectoryPath);
    Directory.CreateDirectory(_directory);
    _manifestPath = Path.Combine(_directory, _manifestName + ".json");
    _ownership = new(Path.Combine(_directory, _manifestName + ".lock"), FileMode.OpenOrCreate,
      FileAccess.ReadWrite, FileShare.None);
    try
    {
      if (File.Exists(_manifestPath))
      {
        using var manifest = File.OpenRead(_manifestPath);
        _active = JsonSerializer.Deserialize<FileGenerationManifest>(manifest)
          ?? throw new InvalidDataException("Generation manifest is empty.");
        ValidateManifest(_active);
        if (_active.ObsoleteFiles.Length > 0) CompleteCleanup();
      }
    }
    catch
    {
      _ownership.Dispose();
      throw;
    }
  }

  public Guid? ActiveGeneration
  {
    get { lock (_gate) { EnsureAvailable(); return _active?.Generation; } }
  }

  public static void ReadFile(string path, Action<FileStream> read)
  {
    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    read(file);
  }

  public void PrepareFile(Guid generation, string fileName, Action<FileStream> write)
  {
    lock (_gate)
    {
      EnsureAvailable();
      if (generation == Guid.Empty || generation == _active?.Generation)
        throw new ArgumentException("Preparation requires an inactive generation.", nameof(generation));
      using var file = new FileStream(GetFilePath(generation, fileName), FileMode.CreateNew,
        FileAccess.Write, FileShare.None);
      write(file);
      file.Flush(true);
    }
  }

  public void CopyFile(Guid generation, string fileName, string sourcePath) =>
    ReadFile(sourcePath, source => PrepareFile(generation, fileName, destination => source.CopyTo(destination)));

  public IReadOnlyDictionary<string, string> GetActiveFiles()
  {
    lock (_gate)
    {
      EnsureAvailable();
      return _active is null ? new Dictionary<string, string>() : Paths(_active);
    }
  }

  public Guid CreateGeneration()
  {
    lock (_gate)
    {
      EnsureAvailable();
      var generation = Guid.CreateVersion7();
      Directory.CreateDirectory(Path.Combine(_directory, generation.ToString("N")));
      return generation;
    }
  }

  public string GetFilePath(Guid generation, string fileName)
  {
    ValidateFileName(fileName);
    return Path.Combine(_directory, generation.ToString("N"), fileName);
  }

  /// <summary>
  /// Verifies domain data before publication and records the exact obsolete files.
  /// Obsolete paths are relative to the owned directory: a source filename or
  /// a generation ID in N format followed by a filename. No directories are deleted.
  /// Writers and readers of source and destination files must be closed by the caller.
  /// </summary>
  public void Activate(Guid generation, IReadOnlyCollection<string> fileNames,
    IReadOnlyCollection<string> obsoleteFiles, Action<IReadOnlyDictionary<string, string>> verify)
  {
    lock (_gate)
    {
      EnsureAvailable();
      ArgumentNullException.ThrowIfNull(verify);
      if (generation == Guid.Empty || generation == _active?.Generation || fileNames.Count == 0)
        throw new ArgumentException("Activation requires a new, nonempty generation.", nameof(generation));

      var checksums = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var name in fileNames)
      {
        ValidateFileName(name);
        checksums.Add(name, string.Empty);
      }
      var candidate = new FileGenerationManifest(1, generation, checksums, obsoleteFiles.ToArray());
      ValidateManifest(candidate);
      var paths = Paths(candidate);
      verify(paths);
      foreach (var name in checksums.Keys.ToArray())
      {
        using var file = new FileStream(paths[name], FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        file.Flush(true);
        checksums[name] = Convert.ToHexString(SHA256.HashData(file));
      }

      try
      {
        Publish(candidate);
        _active = candidate;
        CompleteCleanup();
      }
      catch
      {
        _failed = true;
        throw;
      }
    }
  }

  private void CompleteCleanup()
  {
    foreach (var (name, expected) in _active.Checksums)
    {
      using var file = File.OpenRead(GetFilePath(_active.Generation, name));
      if (Convert.ToHexString(SHA256.HashData(file)) != expected)
        throw new InvalidDataException("Active generation verification failed; source cleanup was not performed.");
    }
    foreach (var obsolete in _active.ObsoleteFiles)
      File.Delete(ObsoletePath(obsolete));

    if (_active.ObsoleteFiles.Length == 0) return;
    var cleaned = _active with { ObsoleteFiles = [] };
    Publish(cleaned);
    _active = cleaned;
  }

  private void Publish(FileGenerationManifest manifest)
  {
    var pending = _manifestPath + ".pending";
    using (var file = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
    {
      JsonSerializer.Serialize(file, manifest);
      file.Flush(true);
    }
    File.Move(pending, _manifestPath, overwrite: true);
  }

  private Dictionary<string, string> Paths(FileGenerationManifest manifest) =>
    manifest.Checksums.Keys.ToDictionary(name => name, name => GetFilePath(manifest.Generation, name));

  private void ValidateManifest(FileGenerationManifest manifest)
  {
    if (manifest.Version != 1 || manifest.Generation == Guid.Empty
      || manifest.Checksums is null || manifest.Checksums.Count == 0 || manifest.ObsoleteFiles is null)
      throw new InvalidDataException("Generation manifest is invalid.");
    foreach (var name in manifest.Checksums.Keys) ValidateFileName(name);
    foreach (var obsolete in manifest.ObsoleteFiles)
    {
      var path = ObsoletePath(obsolete);
      if (path.StartsWith(Path.Combine(_directory, manifest.Generation.ToString("N")) + Path.DirectorySeparatorChar,
        StringComparison.Ordinal))
        throw new InvalidDataException("Source cleanup cannot target the active generation.");
    }
  }

  private string ObsoletePath(string relative)
  {
    var parts = relative.Split('/');
    if (parts.Length == 1)
    {
      ValidateFileName(parts[0]);
      return Path.Combine(_directory, parts[0]);
    }
    if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var generation))
      throw new InvalidDataException("Obsolete file path must identify an owned source file.");
    return GetFilePath(generation, parts[1]);
  }

  private void ValidateFileName(string name)
  {
    if (string.IsNullOrWhiteSpace(name) || name is "." or ".."
      || name.Contains('/') || name.Contains('\\') || Path.IsPathRooted(name)
      || name.StartsWith(_manifestName + ".", StringComparison.Ordinal))
      throw new ArgumentException("A non-reserved file name is required.", nameof(name));
  }

  private void EnsureAvailable()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if (_failed) throw new InvalidOperationException("Reopen the generation owner after a failed publication or cleanup.");
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed) return;
      _disposed = true;
      _ownership.Dispose();
    }
  }
}