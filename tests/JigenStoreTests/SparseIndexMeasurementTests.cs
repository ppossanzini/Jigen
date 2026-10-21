using System.Diagnostics;
using Jigen.DataStructures;
using Jigen.Indexers;
using Xunit.Abstractions;

namespace JigenTests;

/// <summary>
/// K-42: the memory cost of the term index at 100,000 entries is measured, not
/// assumed. The ceiling asserted here is the one declared in the configuration
/// documentation: raising it is a decision that needs a new measurement.
/// </summary>
public class SparseIndexMeasurementTests(ITestOutputHelper output)
{
  private const int Entries = 100_000;
  private const int Vocabulary = 50_000;
  private const int TermsPerEntry = 6;

  /// <summary>
  /// Declared limit, derived from the measurement below: 624 bytes per entry
  /// observed at 100,000 entries, plus a headroom for the shape of a real
  /// vocabulary. Above it the index must move to disk instead of growing in
  /// memory (K-42).
  /// </summary>
  private const double MaxBytesPerEntry = 800;

  [Fact]
  public void IndexAtOneHundredThousandEntries_StaysWithinTheDeclaredFootprint()
  {
    var keys = new byte[Entries][];
    var vectors = new SparseVector[Entries];
    for (var i = 0; i < Entries; i++)
    {
      keys[i] = BitConverter.GetBytes(i);
      vectors[i] = VectorFor(i);
    }

    var index = new SparseIndex();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var heapBefore = GC.GetTotalMemory(true);

    var stopwatch = Stopwatch.StartNew();
    index.Rebuild("bench", keys.Select((key, i) => (key, vectors[i])));
    stopwatch.Stop();

    var heapAfter = GC.GetTotalMemory(true);

    var info = index.GetInfo("bench");
    var heapBytes = heapAfter - heapBefore;
    var bytesPerEntry = info.MemoryBytes / (double)Entries;
    var heapBytesPerEntry = heapBytes / (double)Entries;

    output.WriteLine($"entries            {Entries:N0}");
    output.WriteLine($"distinct terms     {info.Terms:N0}");
    output.WriteLine($"postings           {info.Postings:N0}");
    output.WriteLine($"avg postings/term  {info.AveragePostingsPerTerm:F2}");
    output.WriteLine($"build time         {stopwatch.ElapsedMilliseconds:N0} ms");
    output.WriteLine($"model bytes        {info.MemoryBytes:N0} ({bytesPerEntry:F1} B/entry)");
    output.WriteLine($"measured heap      {heapBytes:N0} ({heapBytesPerEntry:F1} B/entry)");

    // The model is deliberately conservative; the measured heap is the truth.
    Assert.InRange(bytesPerEntry, 1, MaxBytesPerEntry);
    Assert.InRange(heapBytesPerEntry, 1, MaxBytesPerEntry);
    Assert.True(stopwatch.ElapsedMilliseconds < 30_000,
      $"Building {Entries:N0} entries took {stopwatch.ElapsedMilliseconds} ms, above the 30 s budget.");
  }

  [Fact]
  public void SearchAtOneHundredThousandEntries_StaysWithinTheDeclaredBudget()
  {
    var keys = new byte[Entries][];
    var entries = new (byte[], SparseVector)[Entries];
    for (var i = 0; i < Entries; i++)
    {
      keys[i] = BitConverter.GetBytes(i);
      entries[i] = (keys[i], VectorFor(i));
    }

    var index = new SparseIndex();
    index.Rebuild("bench", entries);

    var query = SparseVector.Create([.. Enumerable.Range(0, TermsPerEntry).Select(t => t * 97)], WeightList());

    // Warm up, then average over enough queries to smooth the JIT and the GC.
    index.Search("bench", query, 10);

    const int Runs = 50;
    var stopwatch = Stopwatch.StartNew();
    for (var i = 0; i < Runs; i++) index.Search("bench", query, 10);
    stopwatch.Stop();

    var average = stopwatch.Elapsed.TotalMilliseconds / Runs;
    output.WriteLine($"term list lengths  {index.GetInfo("bench").AveragePostingsPerTerm:F1} on average");
    output.WriteLine($"search average     {average:F3} ms over {Runs} runs");

    Assert.True(average < 250, $"An exact lexical search averaged {average:F1} ms at {Entries:N0} entries.");
  }

  /// <summary>
  /// A skewed distribution: a few terms appear everywhere, most appear rarely.
  /// A flat distribution would make the average term list short and hide the
  /// cost of the frequent terms, which is what dominates the footprint.
  /// </summary>
  private static SparseVector VectorFor(int entry)
  {
    var state = (uint)(entry * 2654435761 + 1);
    var terms = new SortedSet<int>();
    while (terms.Count < TermsPerEntry)
    {
      state = state * 1664525 + 1013904223;
      var unit = (state >> 8) / (double)(1 << 24);
      terms.Add((int)(Vocabulary * Math.Pow(unit, 2.5)));
    }

    var termList = terms.ToArray();
    var weights = new float[termList.Length];
    for (var i = 0; i < termList.Length; i++)
      weights[i] = 1f + i * 0.25f;
    return SparseVector.Create(termList, weights);
  }

  private static float[] WeightList()
  {
    var weights = new float[TermsPerEntry];
    for (var i = 0; i < weights.Length; i++) weights[i] = 1f;
    return weights;
  }
}
