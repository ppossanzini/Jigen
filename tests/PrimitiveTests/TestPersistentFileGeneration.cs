using Jigen.Persistance;
using System.Security.Cryptography;
using System.Text.Json;

namespace PrimitiveTests;

public sealed class TestPersistentFileGeneration : IDisposable
{
  private readonly string _directory;
  private readonly FileGenerationOptions _options;

  public TestPersistentFileGeneration()
  {
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null) throw new DirectoryNotFoundException("Repository root not found.");
    _directory = Path.Combine(root.FullName, ".temp", "file-generation-tests", Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_directory);
    _options = new() { DirectoryPath = _directory };
  }

  [Fact]
  public void Activation_PreservesVerifiedFilesAndDeletesOnlyListedSources_AfterReopen()
  {
    var source = Path.Combine(_directory, "legacy.bin");
    var unrelated = Path.Combine(_directory, "unrelated.bin");
    File.WriteAllBytes(source, [1]);
    File.WriteAllBytes(unrelated, [2]);
    Guid generation;
    using (var owner = new PersistentFileGeneration(_options))
    {
      Assert.Null(owner.ActiveGeneration);
      Assert.Empty(owner.GetActiveFiles());
      generation = owner.CreateGeneration();
      File.WriteAllBytes(owner.GetFilePath(generation, "contents.bin"), [3, 4]);
      File.WriteAllBytes(owner.GetFilePath(generation, "index.bin"), [5, 6]);
      owner.Activate(generation, ["contents.bin", "index.bin"], ["legacy.bin"], files =>
      {
        Assert.True(File.Exists(source));
        Assert.Null(owner.ActiveGeneration);
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(files["contents.bin"]));
        Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(files["index.bin"]));
      });
      Assert.Equal(generation, owner.ActiveGeneration);
      Assert.False(File.Exists(source));
    }
    File.WriteAllBytes(source, [7]);
    using var reopened = new PersistentFileGeneration(_options);
    Assert.Equal(generation, reopened.ActiveGeneration);
    Assert.Equal(2, reopened.GetActiveFiles().Count);
    Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(reopened.GetActiveFiles()["contents.bin"]));
    Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(source));
    Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(unrelated));
  }

  [Fact]
  public void FailedDomainVerification_KeepsPreviousGenerationAndSources()
  {
    using var owner = new PersistentFileGeneration(_options);
    var previous = owner.CreateGeneration();
    File.WriteAllBytes(owner.GetFilePath(previous, "contents.bin"), [1]);
    owner.Activate(previous, ["contents.bin"], [], _ => { });
    var candidate = owner.CreateGeneration();
    File.WriteAllBytes(owner.GetFilePath(candidate, "contents.bin"), [2]);
    var manifest = File.ReadAllBytes(Path.Combine(_directory, "generation.json"));
    Assert.Throws<InvalidDataException>(() => owner.Activate(candidate, ["contents.bin"],
      [$"{previous:N}/contents.bin"], _ => throw new InvalidDataException("Domain verification failed.")));
    Assert.Equal(previous, owner.ActiveGeneration);
    Assert.Equal(manifest, File.ReadAllBytes(Path.Combine(_directory, "generation.json")));
    Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(owner.GetActiveFiles()["contents.bin"]));
    owner.Activate(candidate, ["contents.bin"], [$"{previous:N}/contents.bin"], _ => { });
    Assert.Equal(candidate, owner.ActiveGeneration);
    Assert.False(File.Exists(owner.GetFilePath(previous, "contents.bin")));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(2)]
  public void PendingCleanup_ReopensOnlyWhenDestinationIsIntact(int damage)
  {
    var generation = CreatePendingCleanup();
    var target = Path.Combine(_directory, generation.ToString("N"), "contents.bin");
    var remaining = Path.Combine(_directory, "remaining.bin");
    Assert.False(File.Exists(Path.Combine(_directory, "first.bin")));
    Assert.True(File.Exists(remaining));
    Directory.Delete(Path.Combine(_directory, "blocked.bin"));
    if (damage == 1) File.WriteAllBytes(target, [99]);
    if (damage == 2) File.Delete(target);

    if (damage != 0)
    {
      if (damage == 1)
        Assert.Throws<InvalidDataException>(() => new PersistentFileGeneration(_options));
      else
        Assert.Throws<FileNotFoundException>(() => new PersistentFileGeneration(_options));
      Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(remaining));
      File.WriteAllBytes(target, [3, 4]);
    }

    using (var reopened = new PersistentFileGeneration(_options))
    {
      Assert.Equal(generation, reopened.ActiveGeneration);
      Assert.False(File.Exists(remaining));
      Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(reopened.GetActiveFiles()["contents.bin"]));
    }
    File.WriteAllBytes(remaining, [8]);
    using var cleanReopen = new PersistentFileGeneration(_options);
    Assert.Equal(generation, cleanReopen.ActiveGeneration);
    Assert.Equal(new byte[] { 8 }, File.ReadAllBytes(remaining));
  }

  private Guid CreatePendingCleanup()
  {
    using var owner = new PersistentFileGeneration(_options);
    var generation = owner.CreateGeneration();
    File.WriteAllBytes(owner.GetFilePath(generation, "contents.bin"), [3, 4]);
    File.WriteAllBytes(Path.Combine(_directory, "first.bin"), [1]);
    Directory.CreateDirectory(Path.Combine(_directory, "blocked.bin"));
    File.WriteAllBytes(Path.Combine(_directory, "remaining.bin"), [2]);
    Assert.Throws<UnauthorizedAccessException>(() => owner.Activate(generation, ["contents.bin"],
      ["first.bin", "blocked.bin", "remaining.bin"], _ => { }));
    Assert.Throws<InvalidOperationException>(() => owner.GetActiveFiles());
    return generation;
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void UnpublishedManifest_PreservesPreviousStateAndSources(bool previousGeneration)
  {
    Guid? previous = null;
    Guid candidate;
    var source = Path.Combine(_directory, "legacy.bin");
    File.WriteAllBytes(source, [7]);
    using (var owner = new PersistentFileGeneration(_options))
    {
      if (previousGeneration)
      {
        previous = owner.CreateGeneration();
        File.WriteAllBytes(owner.GetFilePath(previous.Value, "contents.bin"), [1]);
        owner.Activate(previous.Value, ["contents.bin"], [], _ => { });
      }
      candidate = owner.CreateGeneration();
      File.WriteAllBytes(owner.GetFilePath(candidate, "contents.bin"), [2]);
    }
    var pending = JsonSerializer.SerializeToUtf8Bytes(new
    {
      Version = 1, Generation = candidate,
      Checksums = new Dictionary<string, string>
      {
        ["contents.bin"] = Convert.ToHexString(SHA256.HashData(new byte[] { 2 }))
      },
      ObsoleteFiles = new[] { "legacy.bin" }
    });
    using (var file = new FileStream(Path.Combine(_directory, "generation.json.pending"),
      FileMode.Create, FileAccess.Write, FileShare.None))
    {
      file.Write(pending);
      file.Flush(true);
    }
    using var reopened = new PersistentFileGeneration(_options);
    Assert.Equal(previous, reopened.ActiveGeneration);
    Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(source));
    if (previousGeneration)
      Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(reopened.GetActiveFiles()["contents.bin"]));
    else
      Assert.Empty(reopened.GetActiveFiles());
    reopened.Activate(candidate, ["contents.bin"], ["legacy.bin"], _ => { });
    Assert.Equal(candidate, reopened.ActiveGeneration);
    Assert.False(File.Exists(source));
  }

  [Theory]
  [InlineData("../outside.bin", typeof(InvalidDataException))]
  [InlineData("generation.json", typeof(ArgumentException))]
  [InlineData("generation.lock", typeof(ArgumentException))]
  [InlineData("../contents.bin/extra", typeof(InvalidDataException))]
  public void CleanupOutsideOwnedSources_IsRejectedBeforePublication(string obsolete, Type exceptionType)
  {
    using var owner = new PersistentFileGeneration(_options);
    var generation = owner.CreateGeneration();
    File.WriteAllBytes(owner.GetFilePath(generation, "contents.bin"), [1]);
    Assert.Throws(exceptionType, () => owner.Activate(generation, ["contents.bin"], [obsolete], _ => { }));
    Assert.Null(owner.ActiveGeneration);
    Assert.False(File.Exists(Path.Combine(_directory, "generation.json")));
    Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(owner.GetFilePath(generation, "contents.bin")));
  }

  [Fact]
  public void CleanupOfDestination_IsRejectedBeforePublication()
  {
    using var owner = new PersistentFileGeneration(_options);
    var generation = owner.CreateGeneration();
    File.WriteAllBytes(owner.GetFilePath(generation, "contents.bin"), [1]);
    Assert.Throws<InvalidDataException>(() => owner.Activate(generation, ["contents.bin"],
      [$"{generation:N}/contents.bin"], _ => { }));
    Assert.Null(owner.ActiveGeneration);
    Assert.True(File.Exists(owner.GetFilePath(generation, "contents.bin")));
  }

  public void Dispose()
  {
    if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
  }
}