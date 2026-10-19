using System.Buffers.Binary;
using System.Diagnostics;
using Jigen.Persistance;
using Xunit.Abstractions;

namespace PrimitiveTests;

public sealed class TestPersistentBTree : IDisposable
{
  private readonly string _directory;
  private readonly DiskIndexOptions _options;
  private readonly ITestOutputHelper _output;

  public TestPersistentBTree(ITestOutputHelper output)
  {
    _output = output;
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null) throw new InvalidOperationException("Repository root not found.");
    _directory = Path.Combine(root.FullName, ".temp", "persistent-tree-tests",
      Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_directory);
    _options = new() { FilePath = Path.Combine(_directory, "tree.bin"), ViewSize = 65536 };
  }

  [Fact]
  public void EmptyZeroAndPrefixKeys_AreDistinctAfterReopen()
  {
    byte[][] keys = [[0], [0, 0], [0, 0, 0], [0, 1], [255], [255, 0]];
    using (var tree = new PersistentBTree(_options))
    {
      Assert.False(tree.TryGet([0], out _));
      Assert.False(tree.Delete([0]));
      for (var index = keys.Length - 1; index >= 0; index--) tree.Set(keys[index], index);
    }
    using var reopened = new PersistentBTree(_options);
    for (var index = 0; index < keys.Length; index++)
    {
      Assert.True(reopened.TryGet(keys[index], out var value));
      Assert.Equal(index, value);
    }
    Assert.False(reopened.TryGet([0, 0, 1], out _));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RecreateExistingFile_DiscardsPreviousStateAndPersistsNewCommit(bool corrupt)
  {
    using (var tree = new PersistentBTree(_options))
    {
      for (var identity = 0; identity < 50; identity++) tree.Set(Key(identity), identity);
      tree.Set([255], 10);
    }
    if (corrupt) File.WriteAllBytes(_options.FilePath, new byte[19]);
    var previousBytes = File.ReadAllBytes(_options.FilePath);
    if (corrupt)
      Assert.Throws<InvalidDataException>(() => new PersistentBTree(_options));
    else
    {
      using var preserved = new PersistentBTree(_options);
      Assert.True(preserved.TryGet([255], out var value));
      Assert.Equal(10L, value);
      for (var identity = 0; identity < 50; identity++)
      {
        Assert.True(preserved.TryGet(Key(identity), out var stored));
        Assert.Equal(identity, stored);
      }
    }
    Assert.Equal(previousBytes, File.ReadAllBytes(_options.FilePath));

    var recreateOptions = new DiskIndexOptions
    {
      FilePath = _options.FilePath, ViewSize = _options.ViewSize, Recreate = true
    };
    using (var recreated = new PersistentBTree(recreateOptions))
    {
      Assert.False(recreated.TryGet([255], out _));
      for (var identity = 0; identity < 50; identity++)
        Assert.False(recreated.TryGet(Key(identity), out _));
      Assert.Equal(12288L, new FileInfo(_options.FilePath).Length);
      recreated.Set([254], 30);
    }
    using var reopened = new PersistentBTree(_options);
    Assert.False(reopened.TryGet([255], out _));
    for (var identity = 0; identity < 50; identity++)
      Assert.False(reopened.TryGet(Key(identity), out _));
    Assert.True(reopened.TryGet([254], out var restored));
    Assert.Equal(30L, restored);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(65537)]
  [InlineData(1048577)]
  public void RecreateWithInvalidViewSize_DoesNotTruncateExistingFile(int viewSize)
  {
    using (var tree = new PersistentBTree(_options)) tree.Set([1], 10);
    var previousBytes = File.ReadAllBytes(_options.FilePath);
    var invalidOptions = new DiskIndexOptions
    {
      FilePath = _options.FilePath, ViewSize = viewSize, Recreate = true
    };
    Assert.Throws<ArgumentOutOfRangeException>(() => new PersistentBTree(invalidOptions));
    Assert.Equal(previousBytes, File.ReadAllBytes(_options.FilePath));
    using var reopened = new PersistentBTree(_options);
    Assert.True(reopened.TryGet([1], out var value));
    Assert.Equal(10L, value);
  }

  [Fact]
  public void VariableKeysAndMultilevelSplits_MatchReferenceAfterMutationsAndReopen()
  {
    const int count = 1200;
    Dictionary<int, long> reference = [];
    using (var tree = new PersistentBTree(_options))
    {
      for (var index = 0; index < count; index++)
      {
        var identity = index * 947 % count;
        var key = Key(identity);
        tree.Set(key, identity + 1);
        reference[identity] = identity + 1;
        key.AsSpan().Fill(255);
      }
      for (var identity = 0; identity < count; identity += 11)
      {
        tree.Set(Key(identity), -identity - 1);
        reference[identity] = -identity - 1;
      }
      for (var identity = 0; identity < count; identity += 7)
      {
        Assert.True(tree.Delete(Key(identity)));
        Assert.False(tree.Delete(Key(identity)));
        reference.Remove(identity);
      }
      for (var identity = 0; identity < count; identity += 35)
      {
        tree.Set(Key(identity), long.MinValue);
        reference[identity] = long.MinValue;
      }
      VerifyReference(tree, reference, count);
    }
    Assert.True(ReadRootLevel() >= 2, "The workload must split internal nodes, not just leaves.");
    using var reopened = new PersistentBTree(_options);
    VerifyReference(reopened, reference, count);
  }

  [Theory]
  [InlineData("root-record")]
  [InlineData("root-page")]
  [InlineData("tail")]
  public void InterruptedFinalCommit_RecoversPreviousRootAndAllowsFurtherCommit(string damage)
  {
    using (var tree = new PersistentBTree(_options))
    {
      tree.Set([1], 10);
      tree.Set([1], 20);
    }
    if (damage == "root-record") DamageByte(24);
    else if (damage == "root-page") DamageByte(new FileInfo(_options.FilePath).Length - 4096 + 64);
    else
    {
      using var stream = new FileStream(_options.FilePath, FileMode.Open, FileAccess.ReadWrite);
      stream.SetLength(stream.Length - 1);
      stream.Flush(true);
    }
    using (var recovered = new PersistentBTree(_options))
    {
      Assert.True(recovered.TryGet([1], out var previous));
      Assert.Equal(10L, previous);
      recovered.Set([1], 30);
    }
    using var reopened = new PersistentBTree(_options);
    Assert.True(reopened.TryGet([1], out var value));
    Assert.Equal(30L, value);
  }

  [Fact]
  public void BothRootRecordsDamaged_RejectOpeningWithoutChangingTheFile()
  {
    using (var tree = new PersistentBTree(_options)) tree.Set([1], 10);
    DamageByte(24);
    DamageByte(4096 + 24);
    var bytes = File.ReadAllBytes(_options.FilePath);
    Assert.Throws<InvalidDataException>(() => new PersistentBTree(_options));
    Assert.Equal(bytes, File.ReadAllBytes(_options.FilePath));
  }

  [Fact]
  public void DamagedDescendant_ReportsCorruptionOnTraversalAndFailedMutationRequiresReopen()
  {
    using (var tree = new PersistentBTree(_options))
    {
      for (var identity = 0; identity < 200; identity++) tree.Set(Key(identity), identity);
      tree.Set([255], 777);
    }
    DamageByte(ReadFirstLeafOffset() + 60);
    using var reopened = new PersistentBTree(_options);
    Assert.True(reopened.TryGet([255], out var healthy));
    Assert.Equal(777L, healthy);
    Assert.Throws<InvalidDataException>(() => reopened.TryGet(Key(0), out _));
    Assert.Throws<InvalidDataException>(() => reopened.Set(Key(0), 30));
    Assert.Throws<InvalidOperationException>(() => reopened.TryGet([255], out _));
  }

  [Fact]
  public void KeyLimitsAndDisposedInstance_RejectOperationsWithoutMutatingFileLength()
  {
    var tree = new PersistentBTree(_options);
    try
    {
      var key = new byte[512];
      tree.Set(key, long.MaxValue);
      Assert.True(tree.TryGet(key, out var value));
      Assert.Equal(long.MaxValue, value);
      var length = new FileInfo(_options.FilePath).Length;
      Assert.Throws<ArgumentOutOfRangeException>(() => tree.Set([], 0));
      Assert.Throws<ArgumentOutOfRangeException>(() => tree.Set(new byte[513], 0));
      Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryGet([], out _));
      Assert.Throws<ArgumentOutOfRangeException>(() => tree.Delete(new byte[513]));
      Assert.Equal(length, new FileInfo(_options.FilePath).Length);
    }
    finally { tree.Dispose(); }
    tree.Dispose();
    Assert.Throws<ObjectDisposedException>(() => tree.TryGet([1], out _));
    Assert.Throws<ObjectDisposedException>(() => tree.Set([1], 0));
    Assert.Throws<ObjectDisposedException>(() => tree.Delete([1]));
  }

  [Fact]
  public async Task ConcurrentReadsDuringSplits_ObserveCompleteValues()
  {
    using var tree = new PersistentBTree(_options);
    tree.Set([255], 777);
    using var ready = new Barrier(3);
    var writer = Task.Run(() =>
    {
      Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(10)));
      for (var identity = 0; identity < 200; identity++) tree.Set(Key(identity), identity);
    });
    var readers = Enumerable.Range(0, 2).Select(reader => Task.Run(() =>
    {
      Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(10)));
      for (var index = 0; index < 300; index++)
      {
        Assert.True(tree.TryGet([255], out var unchanged));
        Assert.Equal(777L, unchanged);
        var identity = (index + reader) % 200;
        if (tree.TryGet(Key(identity), out var value)) Assert.Equal(identity, value);
      }
    })).ToArray();
    await Task.WhenAll([writer, .. readers]);
    for (var identity = 0; identity < 200; identity++)
    {
      Assert.True(tree.TryGet(Key(identity), out var value));
      Assert.Equal(identity, value);
    }
  }

  [Fact]
  public void GrowingDataset_DoesNotRetainAllKeysInManagedMemory()
  {
    using var tree = new PersistentBTree(_options);
    tree.Set(Key(0), 0);
    var before = GC.GetTotalMemory(true);
    var written = 1;
    foreach (var count in new[] { 128, 1024, 4096 })
    {
      for (; written < count; written++) tree.Set(Key(written), written);
      for (var index = 0; index < count; index += 17)
      {
        Assert.True(tree.TryGet(Key(index), out var value));
        Assert.Equal(index, value);
      }
      var retained = GC.GetTotalMemory(true) - before;
      using var process = Process.GetCurrentProcess();
      process.Refresh();
      _output.WriteLine($"Keys={count}; FileBytes={new FileInfo(_options.FilePath).Length}; "
        + $"RetainedHeapDeltaBytes={retained}; RSSBytes={process.WorkingSet64}; "
        + $"PeakRSSBytes={process.PeakWorkingSet64}; ViewBytes={_options.ViewSize}");
      Assert.True(retained < 1024 * 1024,
        $"Growing to {count} keys retained {retained} managed bytes.");
    }
  }

  private static byte[] Key(int identity)
  {
    var key = new byte[4 + identity % 509];
    BinaryPrimitives.WriteInt32BigEndian(key, identity);
    key.AsSpan(4).Fill((byte)(identity % 251));
    return key;
  }

  private static void VerifyReference(PersistentBTree tree, Dictionary<int, long> reference, int count)
  {
    for (var identity = 0; identity < count; identity++)
    {
      Assert.Equal(reference.TryGetValue(identity, out var expected), tree.TryGet(Key(identity), out var value));
      if (reference.ContainsKey(identity)) Assert.Equal(expected, value);
    }
    Assert.False(tree.TryGet(Key(count), out _));
  }

  private int ReadRootLevel()
  {
    var header = new byte[8192];
    using var stream = new FileStream(_options.FilePath, FileMode.Open, FileAccess.Read);
    stream.ReadExactly(header);
    var copy = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16))
      >= BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(4096 + 16)) ? 0 : 4096;
    stream.Position = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(copy + 24)) + 8;
    Span<byte> level = stackalloc byte[4];
    stream.ReadExactly(level);
    return BinaryPrimitives.ReadInt32LittleEndian(level);
  }

  private long ReadFirstLeafOffset()
  {
    using var stream = new FileStream(_options.FilePath, FileMode.Open, FileAccess.Read);
    var headers = new byte[8192];
    stream.ReadExactly(headers);
    var copy = BinaryPrimitives.ReadInt64LittleEndian(headers.AsSpan(16))
      >= BinaryPrimitives.ReadInt64LittleEndian(headers.AsSpan(4096 + 16)) ? 0 : 4096;
    var offset = BinaryPrimitives.ReadInt64LittleEndian(headers.AsSpan(copy + 24));
    var page = new byte[4096];
    while (true)
    {
      stream.Position = offset;
      stream.ReadExactly(page);
      if (BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(8)) == 0) return offset;
      var payload = BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(64));
      var keyLength = BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(66));
      offset = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(payload + keyLength));
    }
  }

  private void DamageByte(long offset)
  {
    using var stream = new FileStream(_options.FilePath, FileMode.Open, FileAccess.ReadWrite);
    stream.Position = offset;
    var value = stream.ReadByte();
    Assert.NotEqual(-1, value);
    stream.Position = offset;
    stream.WriteByte((byte)(value ^ 0xff));
    stream.Flush(true);
  }

  public void Dispose() => Directory.Delete(_directory, true);
}