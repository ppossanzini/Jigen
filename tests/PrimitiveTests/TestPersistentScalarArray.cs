using Jigen.Persistance;
using System.Diagnostics;
using System.IO.Pipes;
using Xunit.Abstractions;

namespace PrimitiveTests;

public sealed class TestPersistentScalarArray : IDisposable
{
  private readonly string _directory;
  private readonly DiskIndexOptions _options;
  private readonly ITestOutputHelper _output;

  public TestPersistentScalarArray(ITestOutputHelper output)
  {
    _output = output;
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null) throw new InvalidOperationException("Repository root not found.");
    _directory = Environment.GetEnvironmentVariable("JIGEN_ARRAY_WORKER_DIRECTORY")
      ?? Path.Combine(root.FullName, ".temp", "persistent-array-tests",
        Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(_directory);
    _options = new() { FilePath = Path.Combine(_directory, "array.bin"), ViewSize = 65536 };
  }

  [Fact]
  public void EmptyAndZeroValue_AreDistinctAfterReopen()
  {
    using (var array = new PersistentScalarArray(_options))
    {
      Assert.False(array.TryGet(0, out _));
      Assert.False(array.Delete(0));
      array.Set(0, 0);
      Assert.True(array.TryGet(0, out var value));
      Assert.Equal(0L, value);
      Assert.False(array.TryGet(1, out _));
    }
    using var reopened = new PersistentScalarArray(_options);
    Assert.True(reopened.TryGet(0, out var restored));
    Assert.Equal(0L, restored);
    Assert.False(reopened.TryGet(1, out _));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RecreateExistingFile_DiscardsPreviousStateAndPersistsNewCommit(bool corrupt)
  {
    using (var array = new PersistentScalarArray(_options))
    {
      array.Set(0, 10);
      array.Set(1024, 20);
    }
    if (corrupt) File.WriteAllBytes(_options.FilePath, new byte[19]);
    var previousBytes = File.ReadAllBytes(_options.FilePath);
    if (corrupt)
      Assert.Throws<InvalidDataException>(() => new PersistentScalarArray(_options));
    else
    {
      using var preserved = new PersistentScalarArray(_options);
      Assert.True(preserved.TryGet(0, out var value));
      Assert.Equal(10L, value);
      Assert.True(preserved.TryGet(1024, out var distant));
      Assert.Equal(20L, distant);
    }
    Assert.Equal(previousBytes, File.ReadAllBytes(_options.FilePath));

    var recreateOptions = new DiskIndexOptions
    {
      FilePath = _options.FilePath, ViewSize = _options.ViewSize, Recreate = true
    };
    using (var recreated = new PersistentScalarArray(recreateOptions))
    {
      Assert.False(recreated.TryGet(0, out _));
      Assert.False(recreated.TryGet(1024, out _));
      Assert.Equal(8192L, new FileInfo(_options.FilePath).Length);
      recreated.Set(1, 30);
    }
    using var reopened = new PersistentScalarArray(_options);
    Assert.False(reopened.TryGet(0, out _));
    Assert.False(reopened.TryGet(1024, out _));
    Assert.True(reopened.TryGet(1, out var restored));
    Assert.Equal(30L, restored);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(65537)]
  [InlineData(1048577)]
  public void RecreateWithInvalidViewSize_DoesNotTruncateExistingFile(int viewSize)
  {
    using (var array = new PersistentScalarArray(_options)) array.Set(0, 10);
    var previousBytes = File.ReadAllBytes(_options.FilePath);
    var invalidOptions = new DiskIndexOptions
    {
      FilePath = _options.FilePath, ViewSize = viewSize, Recreate = true
    };
    Assert.Throws<ArgumentOutOfRangeException>(() => new PersistentScalarArray(invalidOptions));
    Assert.Equal(previousBytes, File.ReadAllBytes(_options.FilePath));
    using var reopened = new PersistentScalarArray(_options);
    Assert.True(reopened.TryGet(0, out var value));
    Assert.Equal(10L, value);
  }

  [Fact]
  public void GrowthAcrossViews_PreservesBoundarySlotsAndUnwrittenSlots()
  {
    long[] ids = [0, 127, 128, 895, 896, 1023, 1024, 2048];
    using (var array = new PersistentScalarArray(_options))
    {
      foreach (var id in ids) array.Set(id, -id - 1);
      foreach (var id in ids)
      {
        Assert.True(array.TryGet(id, out var value));
        Assert.Equal(-id - 1, value);
      }
      Assert.False(array.TryGet(129, out _));
    }
    using var reopened = new PersistentScalarArray(_options);
    foreach (var id in ids)
    {
      Assert.True(reopened.TryGet(id, out var value));
      Assert.Equal(-id - 1, value);
    }
    Assert.False(reopened.TryGet(129, out _));
  }

  [Fact]
  public void UpdatesDeleteAndReactivation_SurviveReopen()
  {
    using (var array = new PersistentScalarArray(_options))
    {
      array.Set(7, long.MinValue);
      array.Set(7, long.MaxValue);
      Assert.True(array.TryGet(7, out var value));
      Assert.Equal(long.MaxValue, value);
      Assert.True(array.Delete(7));
      Assert.False(array.Delete(7));
      array.Set(8, 18);
    }
    using (var reopened = new PersistentScalarArray(_options))
    {
      Assert.False(reopened.TryGet(7, out _));
      Assert.True(reopened.TryGet(8, out var other));
      Assert.Equal(18L, other);
      reopened.Set(7, 27);
    }
    using var reactivated = new PersistentScalarArray(_options);
    Assert.True(reactivated.TryGet(7, out var restored));
    Assert.Equal(27L, restored);
  }

  [Fact]
  public void InterruptedLatestSlotCopy_RecoversPreviousValueAndAllowsFurtherCommit()
  {
    using (var array = new PersistentScalarArray(_options))
    {
      array.Set(0, 10);
      array.Set(0, 20);
    }
    DamageByte(8192 + 16);
    using (var recovered = new PersistentScalarArray(_options))
    {
      Assert.True(recovered.TryGet(0, out var previous));
      Assert.Equal(10L, previous);
      recovered.Set(0, 30);
    }
    using var reopened = new PersistentScalarArray(_options);
    Assert.True(reopened.TryGet(0, out var value));
    Assert.Equal(30L, value);
  }

  [Fact]
  public void BothSlotCopiesDamaged_ReportCorruptionInsteadOfAbsence()
  {
    using (var array = new PersistentScalarArray(_options)) array.Set(0, 10);
    DamageByte(8192 + 16);
    DamageByte(8192 + 4096 + 16);
    using var reopened = new PersistentScalarArray(_options);
    Assert.Throws<InvalidDataException>(() => reopened.TryGet(0, out _));
    Assert.Throws<InvalidDataException>(() => reopened.Set(0, 20));
    Assert.Throws<InvalidOperationException>(() => reopened.TryGet(0, out _));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void InterruptedGrowth_RecoversPreviousCapacity(bool damageHeader)
  {
    using (var array = new PersistentScalarArray(_options))
    {
      array.Set(0, 10);
      array.Set(128, 20);
    }
    if (damageHeader)
      DamageByte(24);
    else
    {
      using var stream = new FileStream(_options.FilePath, FileMode.Open, FileAccess.ReadWrite);
      stream.SetLength(stream.Length - 1);
      stream.Flush(true);
    }
    using (var recovered = new PersistentScalarArray(_options))
    {
      Assert.True(recovered.TryGet(0, out var first));
      Assert.Equal(10L, first);
      Assert.False(recovered.TryGet(128, out _));
      recovered.Set(128, 30);
    }
    using var reopened = new PersistentScalarArray(_options);
    Assert.True(reopened.TryGet(128, out var value));
    Assert.Equal(30L, value);
  }

  [Fact]
  public void BothHeadersDamaged_RejectOpeningWithoutChangingFileLength()
  {
    using (var array = new PersistentScalarArray(_options)) array.Set(0, 10);
    DamageByte(24);
    DamageByte(4096 + 24);
    var length = new FileInfo(_options.FilePath).Length;
    Assert.Throws<InvalidDataException>(() => new PersistentScalarArray(_options));
    Assert.Equal(length, new FileInfo(_options.FilePath).Length);
  }

  [Fact]
  public async Task ConcurrentReadsDuringGrowth_ObserveCompleteValues()
  {
    using (var array = new PersistentScalarArray(_options))
    {
      array.Set(0, 100);
      using var ready = new Barrier(3);
      var writer = Task.Run(() =>
      {
        Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(10)));
        for (var index = 1; index <= 16; index++) array.Set(index * 128, index);
      });
      var readers = Enumerable.Range(0, 2).Select(reader => Task.Run(() =>
      {
        Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(10)));
        for (var iteration = 0; iteration < 500; iteration++)
        {
          Assert.True(array.TryGet(0, out var value));
          Assert.Equal(100L, value);
          var id = ((iteration + reader) % 16 + 1) * 128;
          if (array.TryGet(id, out var written)) Assert.Equal(id / 128, written);
        }
      })).ToArray();
      await Task.WhenAll([writer, .. readers]);
    }
    using var reopened = new PersistentScalarArray(_options);
    for (var index = 1; index <= 16; index++)
    {
      Assert.True(reopened.TryGet(index * 128, out var value));
      Assert.Equal(index, value);
    }
  }

  [Fact]
  public void InvalidIdsAndDisposedInstance_DoNotMutateTheFile()
  {
    var array = new PersistentScalarArray(_options);
    try
    {
      var length = new FileInfo(_options.FilePath).Length;
      Assert.Throws<ArgumentOutOfRangeException>(() => array.Set(-1, 0));
      Assert.Throws<ArgumentOutOfRangeException>(() => array.TryGet(-1, out _));
      Assert.Throws<ArgumentOutOfRangeException>(() => array.Delete(-1));
      Assert.Throws<OverflowException>(() => array.Set(long.MaxValue, 0));
      Assert.Equal(length, new FileInfo(_options.FilePath).Length);
    }
    finally { array.Dispose(); }
    array.Dispose();
    Assert.Throws<ObjectDisposedException>(() => array.TryGet(0, out _));
    Assert.Throws<ObjectDisposedException>(() => array.Set(0, 1));
    Assert.Throws<ObjectDisposedException>(() => array.Delete(0));
  }

  [Fact]
  public void GrowingDataset_DoesNotRetainAManagedIndexProportionalToSlots()
  {
    long[] counts = [10000, 100000, 1000000];
    foreach (var count in counts)
    {
      var options = new DiskIndexOptions
      {
        FilePath = Path.Combine(_directory, $"memory-{count}.bin"),
        ViewSize = _options.ViewSize
      };
      using var array = new PersistentScalarArray(options);
      array.Set(0, 7);
      Assert.True(array.TryGet(0, out _));
      var before = GC.GetTotalMemory(true);
      array.Set(count - 1, count);
      for (var index = 0; index < 1000; index++)
        array.TryGet(index * (count / 1000), out _);
      Assert.True(array.TryGet(count - 1, out var value));
      Assert.Equal(count, value);
      var retained = GC.GetTotalMemory(true) - before;
      using var process = Process.GetCurrentProcess();
      process.Refresh();
      _output.WriteLine($"Slots={count}; FileBytes={new FileInfo(options.FilePath).Length}; "
        + $"RetainedHeapDeltaBytes={retained}; RSSBytes={process.WorkingSet64}; "
        + $"PeakRSSBytes={process.PeakWorkingSet64}; ViewBytes={options.ViewSize}");
      Assert.True(retained < 1024 * 1024,
        $"Growing to {count} slots retained {retained} managed bytes.");
    }
  }

  [Fact]
  public async Task ProcessTerminationDuringRepeatedUpdates_PreservesConfirmedValues()
  {
    var workerFile = Environment.GetEnvironmentVariable("JIGEN_ARRAY_WORKER_FILE");
    if (workerFile is not null)
    {
      using var array = new PersistentScalarArray(new() { FilePath = workerFile });
      array.Set(0, 1);
      array.Set(1, 777);
      array.Set(0, 2);
      using var pipe = new NamedPipeClientStream(".",
        Environment.GetEnvironmentVariable("JIGEN_ARRAY_WORKER_PIPE"), PipeDirection.Out);
      await pipe.ConnectAsync(30000);
      pipe.WriteByte(1);
      await pipe.FlushAsync();
      for (long version = 3; ; version++) array.Set(0, version);
    }

    for (var attempt = 0; attempt < 3; attempt++)
    {
      var path = Path.Combine(_directory, $"terminated-{attempt}.bin");
      var pipeName = $"jigen-array-{Guid.CreateVersion7():N}";
      using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
      var start = new ProcessStartInfo("dotnet")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      start.ArgumentList.Add("vstest");
      start.ArgumentList.Add(typeof(TestPersistentScalarArray).Assembly.Location);
      start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~"
        + "PrimitiveTests.TestPersistentScalarArray.ProcessTerminationDuringRepeatedUpdates");
      start.Environment["JIGEN_ARRAY_WORKER_FILE"] = path;
      start.Environment["JIGEN_ARRAY_WORKER_PIPE"] = pipeName;
      start.Environment["JIGEN_ARRAY_WORKER_DIRECTORY"] = Path.Combine(_directory, $"worker-{attempt}");
      using var child = Process.Start(start)
        ?? throw new InvalidOperationException("Could not start the array worker.");
      var output = child.StandardOutput.ReadToEndAsync();
      var error = child.StandardError.ReadToEndAsync();
      try
      {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await pipe.WaitForConnectionAsync(deadline.Token);
        var signal = new byte[1];
        await pipe.ReadExactlyAsync(signal, deadline.Token);
        Assert.Equal((byte)1, signal[0]);
        child.Kill(true);
        await child.WaitForExitAsync(deadline.Token);
      }
      finally
      {
        if (!child.HasExited) child.Kill(true);
        await child.WaitForExitAsync();
        _output.WriteLine(await output);
        _output.WriteLine(await error);
      }
      using var recovered = new PersistentScalarArray(new() { FilePath = path });
      Assert.True(recovered.TryGet(0, out var version));
      Assert.True(version >= 2, $"Confirmed version 2 was lost: recovered {version}.");
      Assert.True(recovered.TryGet(1, out var unchanged));
      Assert.Equal(777L, unchanged);
      recovered.Set(0, -1);
      Assert.True(recovered.TryGet(0, out var resumed));
      Assert.Equal(-1L, resumed);
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