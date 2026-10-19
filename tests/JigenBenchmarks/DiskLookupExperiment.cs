using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text.Json;

namespace JigenBenchmarks;

internal static class DiskLookupExperiment
{
  private const int PageSize = 4096;
  private const int PageHeaderSize = 16;
  private const int EntrySize = 16;
  private const int EntriesPerPage = (PageSize - PageHeaderSize) / EntrySize;

  public static async Task RunAsync(string[] args)
  {
    if (args[0] == "disk-lookup-worker")
    {
      RunWorker(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), args[5]);
      return;
    }

    var sizes = args.Length > 1 ? args[1].Split(',').Select(int.Parse).ToArray() : [10_000, 100_000];
    var repetitions = args.Length > 2 ? int.Parse(args[2]) : 3;
    var queries = args.Length > 3 ? int.Parse(args[3]) : 20_000;
    if (sizes.Any(count => count < 1 || count > 10_000_000) || repetitions < 1 || queries < 100)
      throw new ArgumentException("Use sizes 1..10000000, repetitions >= 1 and queries >= 100.");

    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "Jigen.sln")))
      root = root.Parent;
    if (root is null) throw new DirectoryNotFoundException("Jigen.sln not found.");
    var directory = Path.Combine(root.FullName, ".temp", "disk-lookup-experiment", Guid.CreateVersion7().ToString("N"));
    Directory.CreateDirectory(directory);
    var results = new List<JsonElement>();
    Console.WriteLine("Exploratory MMF benchmark: 8-byte keys, 8-byte values, warm file cache, single thread.");
    Console.WriteLine("Bulk-built B+ tree vs fixed-capacity linear-probing hash; no dynamic growth or crash recovery.");
    Console.WriteLine("Direct array uses dense numeric IDs, not arbitrary keys. Updates are in-place, not transactional.");
    foreach (var count in sizes)
    {
      for (var repetition = 0; repetition < repetitions; repetition++)
      {
        var kinds = new[] { "btree", "hash", "direct" };
        for (var order = 0; order < kinds.Length; order++)
        {
          var kind = kinds[(order + repetition) % kinds.Length];
          var path = Path.Combine(directory, $"{kind}-{count}-{repetition}.data");
          var start = new ProcessStartInfo("dotnet")
          {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
          };
          foreach (var argument in new[] { Assembly.GetExecutingAssembly().Location, "disk-lookup-worker",
            kind, count.ToString(), queries.ToString(), (4242 + repetition).ToString(), path })
            start.ArgumentList.Add(argument);
          using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot launch worker.");
          var outputTask = process.StandardOutput.ReadToEndAsync();
          var errorTask = process.StandardError.ReadToEndAsync();
          await process.WaitForExitAsync();
          var output = await outputTask;
          var error = await errorTask;
          if (process.ExitCode != 0) throw new InvalidOperationException($"{kind} failed: {error}\n{output}");
          using var document = JsonDocument.Parse(output);
          results.Add(document.RootElement.Clone());
        }
      }
    }

    var report = Path.Combine(directory, "results.json");
    await File.WriteAllTextAsync(report, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("N      kind     hit p50/p95/p99 ns     miss p95 ns  hot p95 ns  update p95 ns  disk MiB  heap KiB  RSS MiB");
    foreach (var group in results.GroupBy(result => (Count: result.GetProperty("Count").GetInt32(),
      Kind: result.GetProperty("Kind").GetString())))
    {
      double Median(string property)
      {
        var values = group.Select(result => result.GetProperty(property).GetDouble()).Order().ToArray();
        return (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2;
      }
      Console.WriteLine(FormattableString.Invariant($"{group.Key.Count,-7}{group.Key.Kind,-9}{Median("HitP50Ns"),6:F0}/{Median("HitP95Ns"),6:F0}/{Median("HitP99Ns"),6:F0} {Median("MissP95Ns"),12:F0} {Median("HotP95Ns"),11:F0} {Median("UpdateP95Ns"),14:F0} {Median("DiskBytes") / 1048576,9:F2} {Median("HeapDeltaBytes") / 1024,9:F1} {Median("RssBytes") / 1048576,8:F1}"));
    }
    Console.WriteLine($"Medians across {repetitions} isolated processes per size/structure; {queries} samples per workload.");
    Console.WriteLine($"Raw measurements, seeds, build costs and logical probes: {report}");
  }

  private static ulong Key(int identity) => Mix((ulong)identity + 1);

  private static ulong Mix(ulong value)
  {
    value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
    value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
    return value ^ (value >> 31);
  }

  private static long Build(string kind, int count, string path, long capacity)
  {
    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
    file.Write(new byte[PageSize]);
    using var header = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true);
    file.Position = 0;
    header.Write(1);
    header.Write(kind == "btree" ? 1 : kind == "hash" ? 2 : 3);
    header.Write((long)count);
    header.Write(capacity);
    header.Write(0L);
    file.Position = PageSize;
    if (kind is "hash" or "direct")
    {
      file.SetLength(PageSize + (kind == "hash" ? capacity : count) * EntrySize);
      using var mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.ReadWrite,
        HandleInheritability.None, leaveOpen: true);
      using var view = mapping.CreateViewAccessor();
      for (var identity = 0; identity < count; identity++)
      {
        var key = Key(identity);
        var slot = kind == "direct" ? identity : (long)(Mix(key) & (ulong)(capacity - 1));
        while (view.ReadUInt64(PageSize + slot * EntrySize) != 0)
          slot = (slot + 1) & (capacity - 1);
        view.Write(PageSize + slot * EntrySize, key);
        view.Write(PageSize + slot * EntrySize + 8, (long)identity);
      }
      view.Flush();
      file.Flush(flushToDisk: true);
      return 0;
    }

    var entries = Enumerable.Range(0, count).Select(identity => (Key: Key(identity), Value: (long)identity)).ToArray();
    Array.Sort(entries, (left, right) => left.Key.CompareTo(right.Key));
    var children = WriteLevel(file, entries, leaf: true);
    while (children.Length > 1)
      children = WriteLevel(file, children, leaf: false);
    file.Position = 24;
    header.Write(children[0].Value);
    file.Flush(flushToDisk: true);
    return children[0].Value;
  }

  private static (ulong Key, long Value)[] WriteLevel(FileStream file, (ulong Key, long Value)[] entries, bool leaf)
  {
    var children = new List<(ulong Key, long Value)>();
    using var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true);
    for (var first = 0; first < entries.Length; first += EntriesPerPage)
    {
      var position = file.Position;
      var length = Math.Min(EntriesPerPage, entries.Length - first);
      writer.Write(leaf ? 1 : 0);
      writer.Write(length);
      writer.Write(0L);
      for (var offset = 0; offset < length; offset++)
      {
        writer.Write(entries[first + offset].Key);
        writer.Write(entries[first + offset].Value);
      }
      writer.Write(new byte[PageSize - PageHeaderSize - length * EntrySize]);
      children.Add((entries[first].Key, position));
    }
    return children.ToArray();
  }

  private static long Find(MemoryMappedViewAccessor view, string kind, long root, long capacity,
    int identity, out int probes)
  {
    var key = Key(identity);
    probes = 0;
    if (kind == "direct")
    {
      probes = 1;
      var position = PageSize + (long)identity * EntrySize;
      return position < view.Capacity && view.ReadUInt64(position) == key ? position + 8 : -1;
    }
    if (kind == "hash")
    {
      var slot = (long)(Mix(key) & (ulong)(capacity - 1));
      for (long attempt = 0; attempt < capacity; attempt++)
      {
        probes++;
        var storedKey = view.ReadUInt64(PageSize + slot * EntrySize);
        if (storedKey == key) return PageSize + slot * EntrySize + 8;
        if (storedKey == 0) return -1;
        slot = (slot + 1) & (capacity - 1);
      }
      return -1;
    }

    var page = root;
    while (true)
    {
      probes++;
      var leaf = view.ReadInt32(page) == 1;
      var length = view.ReadInt32(page + 4);
      var lower = 0;
      var upper = length;
      while (lower < upper)
      {
        var middle = lower + (upper - lower) / 2;
        var candidate = view.ReadUInt64(page + PageHeaderSize + middle * EntrySize);
        if (candidate <= key) lower = middle + 1;
        else upper = middle;
      }
      var entry = page + PageHeaderSize + Math.Max(0, lower - 1) * EntrySize;
      if (leaf) return view.ReadUInt64(entry) == key ? entry + 8 : -1;
      page = view.ReadInt64(entry + 8);
    }
  }

  private static void RunWorker(string kind, int count, int queries, int seed, string path)
  {
    if (kind is not ("btree" or "hash" or "direct")) throw new ArgumentException("Unknown structure.");
    long capacity = 1;
    while (capacity < (long)count * 2) capacity *= 2;
    var allocatedBeforeBuild = GC.GetTotalAllocatedBytes(precise: true);
    var buildTimer = Stopwatch.StartNew();
    var root = Build(kind, count, path, capacity);
    var buildMs = buildTimer.Elapsed.TotalMilliseconds;
    var buildAllocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBeforeBuild;
    var diskBytes = new FileInfo(path).Length;
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var heapBefore = GC.GetTotalMemory(forceFullCollection: true);
    var measurements = new Dictionary<string, double>();
    try
    {
      using (var mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open))
      using (var view = mapping.CreateViewAccessor())
      {
        for (var identity = 0; identity < count; identity++)
        {
          var position = Find(view, kind, root, capacity, identity, out _);
          if (position < 0 || view.ReadInt64(position) != identity)
            throw new InvalidDataException($"Incorrect initial value for {identity}.");
        }
        for (var identity = count; identity < count + 100; identity++)
          if (Find(view, kind, root, capacity, identity, out _) >= 0)
            throw new InvalidDataException("Absent key reported as present.");

        foreach (var workload in new[] { "Hit", "Miss", "Hot", "Update" })
        {
          var warmup = new Random(seed);
          for (var sample = 0; sample < 20_000; sample++)
          {
            var identity = workload == "Miss" ? count + warmup.Next(count) : warmup.Next(count);
            var started = Stopwatch.GetTimestamp();
            var position = Find(view, kind, root, capacity, identity, out _);
            if (position >= 0)
            {
              _ = view.ReadInt64(position);
              if (workload == "Update") view.Write(position, (long)identity);
            }
            _ = Stopwatch.GetTimestamp() - started;
          }
          var random = new Random(seed);
          var samples = new long[queries];
          long totalProbes = 0;
          var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
          for (var sample = 0; sample < queries; sample++)
          {
            var identity = workload == "Miss" ? count + random.Next(count)
              : workload == "Hot" && random.Next(100) < 80 ? random.Next(Math.Max(1, count / 100))
              : random.Next(count);
            var started = Stopwatch.GetTimestamp();
            var position = Find(view, kind, root, capacity, identity, out var probes);
            var value = position < 0 ? -1 : view.ReadInt64(position);
            if (workload == "Update") view.Write(position, -(long)identity - 1);
            samples[sample] = Stopwatch.GetTimestamp() - started;
            totalProbes += probes;
            if ((workload == "Miss" && position >= 0) || (workload != "Miss" &&
              (position < 0 || (value != identity && (workload != "Update" || value != -(long)identity - 1)))))
              throw new InvalidDataException($"Incorrect {workload} lookup.");
          }
          var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
          Array.Sort(samples);
          foreach (var percentile in new[] { 50, 95, 99 })
            measurements[$"{workload}P{percentile}Ns"] = samples[(int)Math.Ceiling(queries * percentile / 100.0) - 1]
              * 1_000_000_000.0 / Stopwatch.Frequency;
          measurements[$"{workload}AllocatedBytes"] = allocated;
          measurements[$"{workload}MeanLogicalProbes"] = (double)totalProbes / queries;
        }
        var timerSamples = new long[1000];
        for (var sample = 0; sample < timerSamples.Length; sample++)
        {
          var started = Stopwatch.GetTimestamp();
          timerSamples[sample] = Stopwatch.GetTimestamp() - started;
        }
        Array.Sort(timerSamples);
        measurements["TimerP50Ns"] = timerSamples[timerSamples.Length / 2] * 1_000_000_000.0 / Stopwatch.Frequency;
        var flushTimer = Stopwatch.StartNew();
        view.Flush();
        using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
          file.Flush(flushToDisk: true);
        measurements["UpdateBatchFlushMs"] = flushTimer.Elapsed.TotalMilliseconds;
        measurements["HeapDeltaBytes"] = GC.GetTotalMemory(forceFullCollection: true) - heapBefore;
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        measurements["RssBytes"] = process.WorkingSet64;
        measurements["PeakRssBytes"] = process.PeakWorkingSet64;
      }

      using (var mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open))
      using (var view = mapping.CreateViewAccessor())
      {
        if (view.ReadInt32(0) != 1 || view.ReadInt64(8) != count)
          throw new InvalidDataException("Incorrect file header.");
        capacity = view.ReadInt64(16);
        root = view.ReadInt64(24);
        var random = new Random(seed);
        for (var sample = 0; sample < queries; sample++)
        {
          var identity = random.Next(count);
          var position = Find(view, kind, root, capacity, identity, out _);
          if (position < 0 || view.ReadInt64(position) != -(long)identity - 1)
            throw new InvalidDataException("Updated value lost after reopen.");
        }
      }
      Console.WriteLine(JsonSerializer.Serialize(new
      {
        Kind = kind, Count = count, Seed = seed, Queries = queries,
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        HashLoadFactor = (double)count / capacity, DiskBytes = diskBytes,
        BuildMs = buildMs, BuildAllocatedBytes = buildAllocated,
        HitP50Ns = measurements["HitP50Ns"], HitP95Ns = measurements["HitP95Ns"], HitP99Ns = measurements["HitP99Ns"],
        MissP95Ns = measurements["MissP95Ns"], HotP95Ns = measurements["HotP95Ns"], UpdateP95Ns = measurements["UpdateP95Ns"],
        HeapDeltaBytes = measurements["HeapDeltaBytes"], RssBytes = measurements["RssBytes"], Measurements = measurements
      }));
    }
    finally
    {
      File.Delete(path);
    }
  }
}