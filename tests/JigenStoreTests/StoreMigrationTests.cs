using System.Text;
using System.Text.Json.Nodes;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace JigenTests;

public sealed class StoreMigrationTests : IDisposable
{
  private readonly string _path;
  private const string Database = "legacy";
  private string Manifest => Path.Combine(_path, Database + ".store-generation.json");

  public StoreMigrationTests()
  {
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null) throw new DirectoryNotFoundException("Repository root not found.");
    _path = Path.Combine(root.FullName, ".temp", "migration-tests", Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_path);
  }

  private Store Open(bool wal = false) => new(new StoreOptions
  {
    DataBaseName = Database, DataBasePath = _path,
    Wal = wal ? new WalOptions { Enabled = true } : null
  });

  private Dictionary<string, byte[]> CreateLegacy(bool wal = false)
  {
    var contentName = Database + ".content.jigen";
    var vectorsName = Database + ".vectors.jigen";
    var indexName = Database + ".index.jigen";
    using (var contents = new BinaryWriter(File.Create(Path.Combine(_path, contentName))))
    using (var vectors = new BinaryWriter(File.Create(Path.Combine(_path, vectorsName))))
    using (var index = new BinaryWriter(File.Create(Path.Combine(_path, indexName))))
    {
      contents.Write(8L);
      vectors.Write(20L);
      vectors.BaseStream.SetLength(20);
      vectors.BaseStream.Position = 20;
      AppendLegacy(contents, vectors, index, "docs", [1], "old"u8.ToArray(), [1f, 0f]);
      AppendLegacy(contents, vectors, index, "docs", [2], "equal"u8.ToArray(), [0.8f, 0.6f]);
      AppendLegacy(contents, vectors, index, "other", [1], "equal"u8.ToArray(), [0f, 1f]);
      AppendLegacy(contents, vectors, index, "docs", [1], "updated"u8.ToArray(), [1f, 0f]);
      AppendLegacy(contents, vectors, index, "docs", [9], "deleted"u8.ToArray(), [-1f, 0f]);
      WriteIndex(index, "docs", [9], -1, -1, 0, 0);
      AppendLegacy(contents, vectors, index, "docs", [3], null, [0.6f, 0.8f]);
    }
    var names = new List<string> { contentName, vectorsName, indexName };
    if (wal)
    {
      var walName = Database + ".wal.jigen";
      File.WriteAllBytes(Path.Combine(_path, walName), []);
      names.Add(walName);
    }
    return names.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(_path, name)));
  }

  private static void AppendLegacy(BinaryWriter contents, BinaryWriter vectors, BinaryWriter index,
    string collection, byte[] id, byte[] content, float[] embedding)
  {
    var contentPosition = 0L;
    if (content is not null)
    {
      contentPosition = contents.BaseStream.Position;
      contents.Write(id.Length);
      contents.Write(id);
      contents.Write(content.Length);
      contents.Write(content);
    }
    var vectorPosition = vectors.BaseStream.Position;
    vectors.Write(id.Length);
    vectors.Write(id);
    foreach (var value in embedding) vectors.Write(value);
    WriteIndex(index, collection, id, contentPosition, vectorPosition, embedding.Length, content?.Length ?? 0);
  }

  private static void WriteIndex(BinaryWriter index, string collection, byte[] id,
    long contentPosition, long vectorPosition, int dimensions, long size)
  {
    var name = Encoding.UTF8.GetBytes(collection);
    index.Write(id.Length);
    index.Write(id);
    index.Write(name.Length);
    index.Write(name);
    index.Write(contentPosition);
    index.Write(vectorPosition);
    index.Write(dimensions);
    index.Write(size);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task FirstOpen_ConvertsLegacyAndReopensWithoutRemigration(bool wal)
  {
    var source = CreateLegacy(wal);
    var store = Open(wal);
    byte[] manifest;
    try
    {
      AssertMigratedData(store);
      Assert.All(source.Keys, name => Assert.False(File.Exists(Path.Combine(_path, name))));
      var vectorFile = store.GetFileNames().Single(file => file.EndsWith(".vectors.jigen", StringComparison.Ordinal));
      Assert.Equal(source[Database + ".vectors.jigen"], File.ReadAllBytes(vectorFile));
      var first = Assert.Single(store.SearchByMetadata("docs", [1f, 0f], 1, 8));
      Assert.Equal(new byte[] { 1 }, first.entry.Id);
      Assert.Equal(2, store.SearchByMetadata("docs", [1f, 0f], 2, 8).Count());
      await store.AppendContent(new VectorEntry
      {
        CollectionName = "docs", Id = [4], Content = "new"u8.ToArray(),
        Embedding = new float[] { 0f, 1f }
      });
      await store.SaveChangesAsync();
      manifest = File.ReadAllBytes(Manifest);
    }
    finally { await store.Close(); store.Dispose(); }

    var reopened = Open(wal);
    try
    {
      AssertMigratedData(reopened);
      Assert.Equal("new"u8.ToArray(), reopened.GetContent("docs", [4]));
      Assert.Equal(manifest, File.ReadAllBytes(Manifest));
      Assert.All(source.Keys, name => Assert.False(File.Exists(Path.Combine(_path, name))));
      Assert.True(await reopened.ShrinkAsync());
      AssertMigratedData(reopened);
    }
    finally { await reopened.Close(); reopened.Dispose(); }
  }

  private static void AssertMigratedData(Store store)
  {
    Assert.Equal("updated"u8.ToArray(), store.GetContent("docs", [1]));
    Assert.Equal("equal"u8.ToArray(), store.GetContent("docs", [2]));
    Assert.Equal("equal"u8.ToArray(), store.GetContent("other", [1]));
    Assert.Null(store.GetContent("docs", [9]));
    Assert.Null(store.GetContent("docs", [3]));
    Assert.Equal(new float[] { 1f, 0f }, store.GetEmbedding("docs", [1]));
    Assert.Equal(new float[] { 0.8f, 0.6f }, store.GetEmbedding("docs", [2]));
    Assert.Equal(new float[] { 0f, 1f }, store.GetEmbedding("other", [1]));
    Assert.Equal(new float[] { 0.6f, 0.8f }, store.GetEmbedding("docs", [3]));
  }

  [Fact]
  public async Task InterruptedConversion_KeepsSourcesAndRestartsAfterRepair()
  {
    var source = CreateLegacy();
    var contentsPath = Path.Combine(_path, Database + ".content.jigen");
    var damaged = source[Database + ".content.jigen"].ToArray();
    damaged[20] = 0;
    File.WriteAllBytes(contentsPath, damaged);
    Assert.Throws<InvalidDataException>(() => Open());
    Assert.False(File.Exists(Manifest));
    Assert.Equal(damaged, File.ReadAllBytes(contentsPath));
    foreach (var (name, bytes) in source.Where(item => item.Key != Database + ".content.jigen"))
      Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_path, name)));
    Assert.NotEmpty(Directory.GetDirectories(_path));
    File.WriteAllBytes(contentsPath, source[Database + ".content.jigen"]);
    var store = Open();
    try { AssertMigratedData(store); }
    finally { await store.Close(); store.Dispose(); }
  }

  [Fact]
  public async Task InterruptedPublication_RestartsFromPreservedLegacySources()
  {
    var source = CreateLegacy();
    var prepared = Open();
    try { AssertMigratedData(prepared); }
    finally { await prepared.Close(); prepared.Dispose(); }
    var originalGeneration = JsonNode.Parse(File.ReadAllBytes(Manifest))["Generation"].GetValue<string>();
    foreach (var (name, bytes) in source) File.WriteAllBytes(Path.Combine(_path, name), bytes);
    File.Move(Manifest, Manifest + ".pending");
    Assert.All(source, item => Assert.Equal(item.Value, File.ReadAllBytes(Path.Combine(_path, item.Key))));

    var restarted = Open();
    try
    {
      AssertMigratedData(restarted);
      Assert.NotEqual(originalGeneration, JsonNode.Parse(File.ReadAllBytes(Manifest))["Generation"].GetValue<string>());
      Assert.All(source.Keys, name => Assert.False(File.Exists(Path.Combine(_path, name))));
    }
    finally { await restarted.Close(); restarted.Dispose(); }
  }

  [Fact]
  public async Task MigratedAndCompactedStore_BackupPreservesBothGenerationManifests()
  {
    CreateLegacy();
    var store = Open();
    string[] files;
    try
    {
      Assert.True(await store.ShrinkAsync());
      AssertMigratedData(store);
      files = store.GetFileNames().ToArray();
      Assert.Equal(2, files.Count(file => file.EndsWith(".json", StringComparison.Ordinal)));
    }
    finally { await store.Close(); store.Dispose(); }
    var backup = Path.Combine(_path, "backup");
    foreach (var file in files)
    {
      var destination = Path.Combine(backup, Path.GetRelativePath(_path, file));
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      File.Copy(file, destination);
    }
    var restored = new Store(new StoreOptions { DataBasePath = backup, DataBaseName = Database });
    try
    {
      AssertMigratedData(restored);
      Assert.True(restored.SharedMetadata.TryGetContent("docs", [1], out var content));
      Assert.Equal("updated"u8.ToArray(), content);
      Assert.True(await restored.ShrinkAsync());
      AssertMigratedData(restored);
    }
    finally { await restored.Close(); restored.Dispose(); }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PublishedGeneration_PendingCleanupResumesOnlyForIntactDestination(bool corrupt)
  {
    var source = CreateLegacy();
    var store = Open();
    string vectorFile;
    try { vectorFile = store.GetFileNames().Single(file => file.EndsWith(".vectors.jigen", StringComparison.Ordinal)); }
    finally { await store.Close(); store.Dispose(); }
    foreach (var (name, bytes) in source) File.WriteAllBytes(Path.Combine(_path, name), bytes);
    File.Delete(Path.Combine(_path, Database + ".index.jigen"));
    var manifest = JsonNode.Parse(File.ReadAllBytes(Manifest));
    manifest["ObsoleteFiles"] = new JsonArray(source.Keys.Select(name => (JsonNode)JsonValue.Create(name)).ToArray());
    File.WriteAllText(Manifest, manifest.ToJsonString());
    var vectors = File.ReadAllBytes(vectorFile);
    if (corrupt)
    {
      File.WriteAllBytes(vectorFile, [99]);
      Assert.Throws<InvalidDataException>(() => Open());
      Assert.Equal(source[Database + ".content.jigen"], File.ReadAllBytes(Path.Combine(_path, Database + ".content.jigen")));
      File.WriteAllBytes(vectorFile, vectors);
    }
    var reopened = Open();
    try
    {
      AssertMigratedData(reopened);
      Assert.All(source.Keys, name => Assert.False(File.Exists(Path.Combine(_path, name))));
      Assert.Empty(JsonNode.Parse(File.ReadAllBytes(Manifest))["ObsoleteFiles"].AsArray());
    }
    finally { await reopened.Close(); reopened.Dispose(); }
  }

  public void Dispose() => Directory.Delete(_path, recursive: true);
}