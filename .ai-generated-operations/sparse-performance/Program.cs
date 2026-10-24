using System.Diagnostics;
using Jigen.Index.Sparse;
using Jigen.Persistance;

var root = Path.GetFullPath(args.Single());
var temporaryRoot = Path.Combine(root, ".temp");
var dataPath = Path.GetFullPath(Path.Combine(temporaryRoot, $"sparse-performance-{Guid.CreateVersion7():N}"));
if (!dataPath.StartsWith(temporaryRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new InvalidOperationException("Benchmark data must remain under .temp.");
Directory.CreateDirectory(dataPath);
try
{
    await Run(dataPath);
}
finally
{
    Directory.Delete(dataPath, recursive: true);
}

static async Task Run(string dataPath)
{
    var random = new Random(12345);
    const int documents = 3000;
    const int termCount = 200;
    const int blockSize = 64;
    const int termsPerVector = 60;

    var postings = new List<SparsePosting>[termCount];
    for (var term = 0; term < termCount; term++)
        postings[term] = [];
    for (var document = 0; document < documents; document++)
    {
        byte[] id = [.. BitConverter.GetBytes(document)];
        var vectors = random.Next(1, 4);
        for (var vector = 0; vector < vectors; vector++)
            foreach (var term in Enumerable.Range(0, termCount).OrderBy(_ => random.Next()).Take(termsPerVector))
                postings[term].Add(new SparsePosting(id, vector, (float)random.NextDouble()));
    }

    var termOptions = new StoreListOptions { FilePath = Path.Combine(dataPath, "terms") };
    var blockOptions = new StoreListOptions { FilePath = Path.Combine(dataPath, "blocks") };
    await using var terms = new StoredList<SparseTermDirectoryEntry, ValueTuple>(termOptions, default);
    await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(blockOptions, default);

    var build = Stopwatch.StartNew();
    for (var term = 0; term < termCount; term++)
    {
        var ordered = postings[term].OrderBy(posting => Convert.ToHexString(posting.DocumentId), StringComparer.Ordinal)
            .ThenBy(posting => posting.VectorIndex).ToArray();
        var chunks = ordered.Chunk(blockSize).ToArray();
        var references = new SparseBlockReference[chunks.Length];
        for (var position = chunks.Length - 1; position >= 0; position--)
        {
            var block = new SparsePostingBlock(chunks[position]);
            references[position] = new SparseBlockReference(blocks.Count, block);
            blocks.Add(block);
        }
        terms.Add(new SparseTermDirectoryEntry(term, references));
    }
    terms.Flush();
    blocks.Flush();
    build.Stop();

    var opened = Stopwatch.StartNew();
    var index = new SparseIndex(terms, blocks);
    opened.Stop();

    var queries = new Dictionary<int, float>[200];
    for (var queryIndex = 0; queryIndex < queries.Length; queryIndex++)
    {
        Dictionary<int, float> query = [];
        for (var term = 0; term < 20; term++)
            query[random.Next(termCount)] = (float)random.NextDouble();
        queries[queryIndex] = query;
    }

    foreach (var query in queries)
        index.Search(query, 10);

    Console.WriteLine($"documents={documents} terms={termCount} postings={postings.Sum(list => list.Count)} blocks={blocks.Count}");
    Console.WriteLine($"build={build.ElapsedMilliseconds} ms  open={opened.ElapsedMilliseconds} ms");

    var results = new List<string>();
    foreach (var k in new[] { 1, 10, 100, int.MaxValue })
    {
        var best = double.MaxValue;
        var hits = 0;
        var roundMeans = new double[3];
        for (var round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            hits = 0;
            foreach (var query in queries)
                hits += index.Search(query, k).Count;
            watch.Stop();
            roundMeans[round] = watch.Elapsed.TotalMilliseconds / queries.Length;
            best = Math.Min(best, roundMeans[round]);
        }
        var label = k == int.MaxValue ? "max" : k.ToString();
        results.Add($"k={label,-4} {best,7:F2} ms/query  ({hits} hits); rounds=[{string.Join(", ", roundMeans.Select(mean => mean.ToString("F3")))}]");
    }
    foreach (var line in results)
        Console.WriteLine(line);

    var memory = GC.GetTotalAllocatedBytes(true);
    foreach (var query in queries)
        index.Search(query, 10);
    Console.WriteLine($"allocated per query k=10: {(GC.GetTotalAllocatedBytes(true) - memory) / (double)queries.Length / 1024:F1} KB");
}
