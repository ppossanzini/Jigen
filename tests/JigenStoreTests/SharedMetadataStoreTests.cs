using System.Diagnostics;
using System.IO.Pipes;
using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Jigen.Persistance;

namespace JigenTests;

public sealed class SharedMetadataStoreTests : IDisposable
{
  private readonly string _path;

  public SharedMetadataStoreTests()
  {
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;

    if (root is null)
      throw new DirectoryNotFoundException("The repository root could not be located.");

    _path = Path.Combine(root.FullName, ".temp", "shared-metadata-tests", Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_path);
  }

  private SharedMetadataStore Open() => Open(_path);

  private static SharedMetadataStore Open(string path) => new(
    new StoreListOptions { FilePath = Path.Combine(path, "contents"), FlushInterval = TimeSpan.FromHours(1) },
    new StoreListOptions { FilePath = Path.Combine(path, "associations"), FlushInterval = TimeSpan.FromHours(1) });

  [Fact]
  public async Task RepeatedCompaction_PreservesSharedAndUnassociatedMetadataIds_AfterReopen()
  {
    int shared;
    int unassociated;
    int appended;
    byte[] updated = "updated"u8.ToArray();
    await using (var store = Open())
    {
      shared = store.AddMetadata(new byte[4096]);
      store.AssociateEmbedding("docs", [1], shared);
      store.AssociateEmbedding("docs", [2], shared);
      Assert.True(store.UpdateMetadata(shared, updated));
      unassociated = store.AddMetadata("unassociated"u8.ToArray());
      var size = store.ContentSize;
      var sources = store.GetFileNames();
      Assert.True(store.DeadBytes > 0);

      store.Compact();
      Assert.True(store.ContentSize < size);
      Assert.Equal(0, store.DeadBytes);
      Assert.All(sources, file => Assert.False(File.Exists(file)));
      Assert.Equal(updated, store.GetContent("docs", [1]));
      Assert.Equal(updated, store.GetContent("docs", [2]));
      Assert.False(store.UpdateMetadata(shared + 1, "invalid version ID"u8.ToArray()));
      Assert.True(store.UpdateMetadata(shared, "second"u8.ToArray()));
      Assert.True(store.DeleteEmbedding("docs", [2]));
      store.AssociateEmbedding("other", [1], shared);
      sources = store.GetFileNames();
      store.Compact();
      Assert.Equal(0, store.DeadBytes);
      Assert.All(sources.Where(file => !store.GetFileNames().Contains(file)),
        file => Assert.False(File.Exists(file)));
      appended = store.AddMetadata("new"u8.ToArray());
      Assert.True(appended > unassociated);
    }

    await using var reopened = Open();
    Assert.Equal("second"u8.ToArray(), reopened.GetContent("docs", [1]));
    Assert.Equal("second"u8.ToArray(), reopened.GetContent("other", [1]));
    Assert.Null(reopened.GetContent("docs", [2]));
    Assert.False(reopened.UpdateMetadata(shared + 1, "invalid"u8.ToArray()));
    reopened.AssociateEmbedding("docs", [2], shared);
    reopened.AssociateEmbedding("docs", [3], unassociated);
    reopened.AssociateEmbedding("docs", [4], appended);
    Assert.Equal("second"u8.ToArray(), reopened.GetContent("docs", [2]));
    Assert.Equal("unassociated"u8.ToArray(), reopened.GetContent("docs", [3]));
    Assert.Equal("new"u8.ToArray(), reopened.GetContent("docs", [4]));
    Assert.True(reopened.UpdateMetadata(shared, updated));
    Assert.Equal(updated, reopened.GetContent("docs", [1]));
    Assert.Equal(updated, reopened.GetContent("docs", [2]));
    Assert.Equal(updated, reopened.GetContent("other", [1]));
  }

  [Fact]
  public async Task CompactedStore_BackupReopensWithSharedIdentityVectorsAndCurrentStatistics()
  {
    var live = Path.Combine(_path, "live");
    var backup = Path.Combine(_path, "backup");
    Directory.CreateDirectory(live);
    Directory.CreateDirectory(backup);
    var options = new StoreOptions { DataBasePath = live, DataBaseName = "backup-test" };
    var store = new Store(options);
    string[] files;
    int metadata;
    byte[] content = "shared-current"u8.ToArray();
    byte[] independent = "independent"u8.ToArray();
    try
    {
      await store.AppendContentBulk([
        new VectorEntry { CollectionName = "docs", Id = [1], Content = new byte[4096], Embedding = new float[] { 1f, 0f } },
        new VectorEntry { CollectionName = "docs", Id = [2], Content = new byte[4096], Embedding = new float[] { 0.8f, 0.6f } },
        new VectorEntry { CollectionName = "other", Id = [1], Content = independent, Embedding = new float[] { 0f, 1f } }
      ]);
      await store.SaveChangesAsync();
      metadata = store.SharedMetadata.AddMetadata(new byte[8192]);
      store.SharedMetadata.AssociateEmbedding("docs", [1], metadata);
      store.SharedMetadata.AssociateEmbedding("docs", [2], metadata);
      Assert.True(store.SharedMetadata.UpdateMetadata(metadata, content));
      Assert.Equal(content.Length, store.GetCollectionInfo("docs").ContentSize);
      Assert.Equal(independent.Length, store.GetCollectionInfo("other").ContentSize);
      var before = store.ContentSize;
      Assert.True(await store.ShrinkAsync());
      Assert.True(store.ContentSize < before);
      Assert.Equal(0, store.DeadBytes);
      var group = Assert.Single(store.SearchByMetadata("docs", [1f, 0f], top: 2, candidateBudget: 16));
      Assert.Equal(metadata, group.metadataIndex);
      Assert.Equal(new byte[] { 1 }, group.entry.Id);
      Assert.Equal(content, group.entry.Content.ToArray());
      Assert.Equal(new float[] { 1f, 0f }, store.GetEmbedding("docs", [1]));
      Assert.Equal(new float[] { 0.8f, 0.6f }, store.GetEmbedding("docs", [2]));
      files = store.GetFileNames().ToArray();
    }
    finally
    {
      await store.Close();
      store.Dispose();
    }

    foreach (var file in files)
    {
      var destination = Path.Combine(backup, Path.GetRelativePath(live, file));
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      File.Copy(file, destination);
    }

    var restored = new Store(new StoreOptions { DataBasePath = backup, DataBaseName = "backup-test" });
    try
    {
      Assert.Equal(0, restored.DeadBytes);
      Assert.Equal(content.Length, restored.GetCollectionInfo("docs").ContentSize);
      Assert.Equal(independent.Length, restored.GetCollectionInfo("other").ContentSize);
      Assert.Equal(content, restored.GetContent("docs", [1]));
      Assert.Equal(content, restored.GetContent("docs", [2]));
      Assert.Equal(independent, restored.GetContent("other", [1]));
      Assert.Equal(new float[] { 1f, 0f }, restored.GetEmbedding("docs", [1]));
      Assert.Equal(new float[] { 0.8f, 0.6f }, restored.GetEmbedding("docs", [2]));
      Assert.Equal(metadata, Assert.Single(restored.SearchByMetadata("docs", [1f, 0f], 2, 16)).metadataIndex);
      Assert.True(restored.SharedMetadata.UpdateMetadata(metadata, "restored-update"u8.ToArray()));
      Assert.Equal("restored-update"u8.ToArray(), restored.GetContent("docs", [1]));
      Assert.Equal("restored-update"u8.ToArray(), restored.GetContent("docs", [2]));
      Assert.True(await restored.DeleteContent("docs", [2]));
      await restored.SaveChangesAsync();
      Assert.True(await restored.ShrinkAsync());
      Assert.Null(restored.GetContent("docs", [2]));
      Assert.Equal(independent, restored.GetContent("other", [1]));
    }
    finally
    {
      await restored.Close();
      restored.Dispose();
    }

    var original = new Store(options);
    try
    {
      Assert.Equal(content, original.GetContent("docs", [1]));
      Assert.Equal(content, original.GetContent("docs", [2]));
    }
    finally
    {
      await original.Close();
      original.Dispose();
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SharedContent_IsReadableByBothEmbeddingIds_AfterReopen(bool explicitFlush)
  {
    byte[] firstId = [1, 2];
    byte[] secondId = [3, 4];
    var expected = "shared content"u8.ToArray();

    await using (var store = Open())
    {
      var metadataIndex = store.AddMetadata(expected);
      store.AssociateEmbedding("docs", firstId, metadataIndex);
      store.AssociateEmbedding("docs", secondId, metadataIndex);
      if (explicitFlush) store.Flush();
    }

    await using var reopened = Open();
    Assert.Equal(expected, reopened.GetContent("docs", firstId));
    Assert.True(reopened.TryGetContent("docs", secondId, out var content));
    Assert.Equal(expected, content);
  }

  [Fact]
  public async Task SameEmbeddingId_InDifferentCollections_ResolvesDistinctContentAfterReopen()
  {
    byte[] embeddingId = [7, 8];

    await using (var store = Open())
    {
      store.AssociateEmbedding("first", embeddingId, store.AddMetadata("first"u8.ToArray()));
      store.AssociateEmbedding("second", embeddingId, store.AddMetadata("second"u8.ToArray()));
    }

    await using var reopened = Open();
    Assert.Equal("first"u8.ToArray(), reopened.GetContent("first", embeddingId));
    Assert.Equal("second"u8.ToArray(), reopened.GetContent("second", embeddingId));
    Assert.Null(reopened.GetContent("missing", embeddingId));
  }

  [Fact]
  public async Task MissingEmbedding_ReturnsAbsence_InEmptyAndPopulatedStore()
  {
    byte[] missingId = [9];

    await using (var store = Open())
    {
      Assert.Null(store.GetContent("docs", missingId));
      Assert.False(store.TryGetContent("docs", missingId, out var absent));
      Assert.Null(absent);
      store.AssociateEmbedding("docs", [1], store.AddMetadata([]));
    }

    await using var reopened = Open();
    Assert.False(reopened.TryGetContent("docs", missingId, out var content));
    Assert.Null(content);
    Assert.True(reopened.TryGetContent("docs", [1], out var empty));
    Assert.Empty(empty);
  }

  [Fact]
  public async Task MutatingCallerBuffers_DoesNotChangeStoredContentOrEmbeddingIdentity()
  {
    byte[] embeddingId = [1, 2];
    byte[] input = [3, 4];

    await using (var store = Open())
    {
      var metadataIndex = store.AddMetadata(input);
      store.AssociateEmbedding("docs", embeddingId, metadataIndex);
      input[0] = 99;
      embeddingId[0] = 99;
      var returned = store.GetContent("docs", [1, 2]);
      Assert.Equal(new byte[] { 3, 4 }, returned);
      returned[0] = 99;
      Assert.Equal(new byte[] { 3, 4 }, store.GetContent("docs", [1, 2]));
    }

    await using var reopened = Open();
    Assert.Equal(new byte[] { 3, 4 }, reopened.GetContent("docs", [1, 2]));
    Assert.Null(reopened.GetContent("docs", embeddingId));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MetadataUpdates_ReachAllAssociatedEmbeddings_AfterReopen(bool explicitFlush)
  {
    await using (var store = Open())
    {
      var metadataIndex = store.AddMetadata("original"u8.ToArray());
      store.AssociateEmbedding("docs", [1], metadataIndex);
      store.AssociateEmbedding("docs", [2], metadataIndex);
      store.AssociateEmbedding("other", [3], store.AddMetadata("unrelated"u8.ToArray()));
      Assert.True(store.UpdateMetadata(metadataIndex, "intermediate"u8.ToArray()));
      Assert.True(store.UpdateMetadata(metadataIndex, "latest"u8.ToArray()));
      Assert.Equal("latest"u8.ToArray(), store.GetContent("docs", [1]));
      Assert.False(store.UpdateMetadata(-1, []));
      Assert.False(store.UpdateMetadata(int.MaxValue, []));
      if (explicitFlush) store.Flush();
    }

    await using var reopened = Open();
    Assert.Equal("latest"u8.ToArray(), reopened.GetContent("docs", [1]));
    Assert.Equal("latest"u8.ToArray(), reopened.GetContent("docs", [2]));
    Assert.Equal("unrelated"u8.ToArray(), reopened.GetContent("other", [3]));
  }

  [Fact]
  public async Task DeletingLastEmbedding_PreservesMetadata_ForReassociationAfterReopen()
  {
    int metadataIndex;
    await using (var store = Open())
    {
      metadataIndex = store.AddMetadata("retained"u8.ToArray());
      store.AssociateEmbedding("docs", [1], metadataIndex);
      store.AssociateEmbedding("docs", [2], metadataIndex);
      Assert.True(store.DeleteEmbedding("docs", [1]));
      Assert.Null(store.GetContent("docs", [1]));
      Assert.Equal("retained"u8.ToArray(), store.GetContent("docs", [2]));
      Assert.False(store.DeleteEmbedding("docs", [1]));
      Assert.False(store.DeleteEmbedding("missing", [2]));
      Assert.True(store.DeleteEmbedding("docs", [2]));
    }

    await using (var reopened = Open())
    {
      Assert.Null(reopened.GetContent("docs", [1]));
      Assert.Null(reopened.GetContent("docs", [2]));
      reopened.AssociateEmbedding("docs", [1], metadataIndex);
      Assert.Equal("retained"u8.ToArray(), reopened.GetContent("docs", [1]));
    }

    await using var finalStore = Open();
    Assert.Equal("retained"u8.ToArray(), finalStore.GetContent("docs", [1]));
    Assert.Null(finalStore.GetContent("docs", [2]));
  }

  [Fact]
  public async Task ConcurrentWriters_PreserveEveryAssociation_AndCompleteContentAfterReopen()
  {
    var expectedById = Enumerable.Range(0, 24)
      .Select(index => (Id: BitConverter.GetBytes(index), Content: BitConverter.GetBytes(index + 100)))
      .ToArray();
    var sharedId = new byte[] { 99 };
    var updated = Enumerable.Repeat((byte)42, 4096).ToArray();

    await using (var store = Open())
    {
      var sharedMetadataIndex = store.AddMetadata(new byte[4096]);
      store.AssociateEmbedding("shared", sharedId, sharedMetadataIndex);
      var writers = expectedById.Select(item => Task.Run(() =>
      {
        var metadataIndex = store.AddMetadata(item.Content);
        store.AssociateEmbedding("docs", item.Id, metadataIndex);
        Assert.True(store.UpdateMetadata(sharedMetadataIndex, updated));
        var observed = store.GetContent("shared", sharedId);
        Assert.Equal(updated, observed);
      })).ToArray();
      await Task.WhenAll(writers);
    }

    await using var reopened = Open();
    foreach (var item in expectedById)
      Assert.Equal(item.Content, reopened.GetContent("docs", item.Id));
    Assert.Equal(updated, reopened.GetContent("shared", sharedId));
  }

  [Fact]
  public async Task ProcessTermination_RecoversCompletedMutation_WithAndWithoutExplicitFlush()
  {
    var childPath = Environment.GetEnvironmentVariable("JIGEN_METADATA_CRASH_PATH");
    if (childPath is not null)
    {
      var childStore = Open(childPath);
      var metadataIndex = childStore.AddMetadata("original"u8.ToArray());
      childStore.AssociateEmbedding("docs", [1], metadataIndex);
      childStore.Flush();
      Assert.True(childStore.UpdateMetadata(metadataIndex, "updated"u8.ToArray()));
      if (Environment.GetEnvironmentVariable("JIGEN_METADATA_CRASH_FLUSH") == "true")
        childStore.Flush();

      using var pipe = new NamedPipeClientStream(".",
        Environment.GetEnvironmentVariable("JIGEN_METADATA_CRASH_PIPE"), PipeDirection.InOut);
      await pipe.ConnectAsync(30000);
      await pipe.WriteAsync(new byte[] { 1 });
      await pipe.FlushAsync();
      await pipe.ReadExactlyAsync(new byte[1]);
      throw new InvalidOperationException("The crash worker must be terminated without disposing the store.");
    }

    foreach (var flush in new[] { false, true })
    {
      var path = Path.Combine(_path, flush ? "flushed" : "committed");
      Directory.CreateDirectory(path);
      var pipeName = $"jigen-metadata-{Guid.CreateVersion7():N}";
      using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
      var startInfo = new ProcessStartInfo("dotnet")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      startInfo.ArgumentList.Add("vstest");
      startInfo.ArgumentList.Add(typeof(SharedMetadataStoreTests).Assembly.Location);
      startInfo.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=JigenTests.SharedMetadataStoreTests.ProcessTermination_RecoversCompletedMutation_WithAndWithoutExplicitFlush");
      startInfo.Environment["JIGEN_METADATA_CRASH_PATH"] = path;
      startInfo.Environment["JIGEN_METADATA_CRASH_PIPE"] = pipeName;
      startInfo.Environment["JIGEN_METADATA_CRASH_FLUSH"] = flush ? "true" : "false";
      using var process = Process.Start(startInfo);
      var output = process.StandardOutput.ReadToEndAsync();
      var errors = process.StandardError.ReadToEndAsync();
      try
      {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await server.WaitForConnectionAsync(timeout.Token);
        var ready = new byte[1];
        await server.ReadExactlyAsync(ready, timeout.Token);
        Assert.Equal(1, ready[0]);
      }
      finally
      {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        await output;
        await errors;
      }

      await using var reopened = Open(path);
      Assert.Equal("updated"u8.ToArray(),
        reopened.GetContent("docs", [1]));
    }
  }

  [Theory]
  [InlineData("associations")]
  [InlineData("associations.index")]
  public async Task InterruptedAssociationRecord_PreservesReadableSharedMetadata(string interruptedFile)
  {
    await using (var store = Open())
    {
      var metadataIndex = store.AddMetadata("original"u8.ToArray());
      store.AssociateEmbedding("docs", [1], metadataIndex);
      store.Flush();
      Assert.True(store.UpdateMetadata(metadataIndex, "updated"u8.ToArray()));
      store.Flush();
    }

    using (var stream = new FileStream(Path.Combine(_path, interruptedFile),
      FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
      stream.SetLength(stream.Length - 1);
      stream.Flush(flushToDisk: true);
    }

    await using (var reopened = Open())
    {
      Assert.Equal("original"u8.ToArray(), reopened.GetContent("docs", [1]));
      Assert.True(reopened.UpdateMetadata(0, "after recovery"u8.ToArray()));
      reopened.Flush();
    }

    await using var finalStore = Open();
    Assert.Equal("after recovery"u8.ToArray(), finalStore.GetContent("docs", [1]));
  }

  public void Dispose() => Directory.Delete(_path, recursive: true);
}