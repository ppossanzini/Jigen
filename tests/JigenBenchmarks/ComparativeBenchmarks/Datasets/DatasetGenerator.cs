using System.Runtime.InteropServices;

namespace JigenBenchmarks.Comparative.Datasets;

/// <summary>
/// Generates or loads benchmark datasets shared across all vector DB adapters.
/// Supports random uniform vectors and downloads standard ANN-benchmarks datasets.
/// All data is stored under /tmp/benchmark-data/ so Docker containers can mount it.
/// </summary>
public static class DatasetGenerator
{
    public const string DataDir = "/tmp/benchmark-data";

    public record Dataset(
        string Name,
        int Count,
        int Dimension,
        float[][] TrainVectors,
        float[][] TestVectors,
        float[][] GroundTruth,  // [queryIdx][k] = indices in train
        string[]? TrainIds,
        Dictionary<string, object>?[]? TrainMetadata);

    /// <summary>
    /// Generate random unit vectors (uniform on hypersphere).
    /// Same seed = same data across all DBs = fair comparison.
    /// </summary>
    public static Dataset GenerateRandom(string name, int count, int dim, int queries = 1000, int gtK = 100, int seed = 42)
    {
        var rng = new Random(seed);
        var train = new float[count][];
        var ids = new string[count];
        for (int i = 0; i < count; i++)
        {
            train[i] = CreateUnitVector(rng, dim);
            ids[i] = Guid.CreateVersion7().ToString();
        }

        var test = new float[queries][];
        for (int i = 0; i < queries; i++)
            test[i] = CreateUnitVector(rng, dim);

        // Ground truth via brute force
        var gt = ComputeGroundTruth(train, test, gtK);

        return new Dataset(name, count, dim, train, test, gt, ids, null);
    }

    /// <summary>
    /// Load standard ANN-benchmarks datasets (SIFT, GIST, GloVe).
    /// Downloads from http://ann-benchmarks.com if not cached.
    /// </summary>
    public static Dataset? LoadAnnBenchmark(string datasetName, int maxVectors = int.MaxValue)
    {
        var dim = datasetName switch
        {
            "sift" or "sift-128-euclidean" => 128,
            "gist" or "gist-960-euclidean" => 960,
            "glove-25" or "glove-25-angular" => 25,
            "glove-50" or "glove-50-angular" => 50,
            "glove-100" or "glove-100-angular" => 100,
            "glove-200" or "glove-200-angular" => 200,
            _ => throw new ArgumentException($"Unknown dataset: {datasetName}")
        };

        Directory.CreateDirectory(DataDir);

        var basePath = Path.Combine(DataDir, datasetName);
        var trainPath = basePath + ".train.fvecs";
        var testPath = basePath + ".test.fvecs";
        var gtPath = basePath + ".groundtruth.ivecs";

        if (!File.Exists(trainPath))
            DownloadAnnDataset(datasetName, basePath);

        var train = ReadFvecs(trainPath, maxVectors);
        var test = ReadFvecs(testPath, 1000);
        var gt = ReadIvecsAsFloatGroundTruth(gtPath, test.Length, 100);

        var ids = new string[train.Length];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = Guid.CreateVersion7().ToString();

        // Pre-normalize for angular distance
        if (datasetName.Contains("angular"))
        {
            foreach (var v in train) NormalizeInPlace(v);
            foreach (var v in test) NormalizeInPlace(v);
        }

        return new Dataset(datasetName, train.Length, dim, train, test, gt, ids, null);
    }

    /// <summary>Save dataset to disk for Docker-shared access.</summary>
    public static void SaveToDisk(Dataset ds)
    {
        var dir = Path.Combine(DataDir, ds.Name);
        Directory.CreateDirectory(dir);

        // Save vectors as raw float32 binary (fast to mmap)
        SaveFloatBinary(Path.Combine(dir, "train.f32"), ds.TrainVectors);
        SaveFloatBinary(Path.Combine(dir, "test.f32"), ds.TestVectors);
        SaveFloatBinaryGroundTruth(Path.Combine(dir, "groundtruth.i32"), ds.GroundTruth);

        // Save IDs and metadata as JSON lines
        if (ds.TrainIds != null)
            File.WriteAllLines(Path.Combine(dir, "ids.jsonl"), ds.TrainIds);

        Console.WriteLine($"Saved dataset '{ds.Name}' ({ds.Count}×{ds.Dimension}) to {dir}");
    }

    /// <summary>Load dataset from disk.</summary>
    public static Dataset LoadFromDisk(string name)
    {
        var dir = Path.Combine(DataDir, name);
        var train = LoadFloatBinary(Path.Combine(dir, "train.f32"));
        var test = LoadFloatBinary(Path.Combine(dir, "test.f32"));
        var gt = LoadGroundTruth(Path.Combine(dir, "groundtruth.i32"));

        int dim = train.Length > 0 ? train[0].Length : 0;
        var ids = File.Exists(Path.Combine(dir, "ids.jsonl"))
            ? File.ReadAllLines(Path.Combine(dir, "ids.jsonl"))
            : null;

        return new Dataset(name, train.Length, dim, train, test, gt, ids, null);
    }

    // ────────── helpers ──────────

    private static float[] CreateUnitVector(Random rng, int dim)
    {
        var v = new float[dim];
        float norm = 0;
        for (int i = 0; i < dim; i++)
        {
            v[i] = (float)(rng.NextDouble() * 2 - 1);
            norm += v[i] * v[i];
        }
        norm = MathF.Sqrt(norm);
        for (int i = 0; i < dim; i++) v[i] /= norm;
        return v;
    }

    private static void NormalizeInPlace(float[] v)
    {
        float norm = 0;
        for (int i = 0; i < v.Length; i++) norm += v[i] * v[i];
        norm = MathF.Sqrt(norm);
        if (norm > 0)
            for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }

    private static float Dot(float[] a, float[] b)
    {
        float sum = 0;
        for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private static float[][] ComputeGroundTruth(float[][] train, float[][] test, int k)
    {
        // Brute-force exact NN for ground truth
        var gt = new float[test.Length][];
        for (int qi = 0; qi < test.Length; qi++)
        {
            var scores = new (float score, int idx)[train.Length];
            for (int ti = 0; ti < train.Length; ti++)
                scores[ti] = (Dot(test[qi], train[ti]), ti);

            Array.Sort(scores, (a, b) => b.score.CompareTo(a.score)); // descending cosine sim
            gt[qi] = new float[k];
            for (int i = 0; i < k; i++)
                gt[qi][i] = scores[i].idx;
        }
        return gt;
    }

    // ────────── binary I/O ──────────

    private static void SaveFloatBinary(string path, float[][] data)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);
        bw.Write(data.Length);       // row count
        if (data.Length > 0)
            bw.Write(data[0].Length); // column count
        foreach (var row in data)
        {
            var bytes = MemoryMarshal.AsBytes<float>(row);
            bw.Write(bytes);
        }
    }

    private static float[][] LoadFloatBinary(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        int rows = br.ReadInt32();
        int cols = br.ReadInt32();
        var data = new float[rows][];
        for (int i = 0; i < rows; i++)
        {
            data[i] = new float[cols];
            var bytes = MemoryMarshal.AsBytes<float>(data[i]).ToArray();
            br.Read(bytes);
        }
        return data;
    }

    private static void SaveFloatBinaryGroundTruth(string path, float[][] gt)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);
        bw.Write(gt.Length);
        bw.Write(gt.Length > 0 ? gt[0].Length : 0);
        foreach (var row in gt)
            foreach (var v in row)
                bw.Write((int)v);
    }

    private static float[][] LoadGroundTruth(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        int queries = br.ReadInt32();
        int k = br.ReadInt32();
        var gt = new float[queries][];
        for (int i = 0; i < queries; i++)
        {
            gt[i] = new float[k];
            for (int j = 0; j < k; j++)
                gt[i][j] = br.ReadInt32();
        }
        return gt;
    }

    // ────────── ANN-benchmarks downloader ──────────

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private static void DownloadAnnDataset(string name, string basePath)
    {
        var baseUrl = $"http://ann-benchmarks.com/{name}";
        Console.WriteLine($"Downloading {name} from {baseUrl}...");

        DownloadFile($"{baseUrl}.train.fvecs", basePath + ".train.fvecs");
        DownloadFile($"{baseUrl}.test.fvecs", basePath + ".test.fvecs");
        DownloadFile($"{baseUrl}.groundtruth.ivecs", basePath + ".groundtruth.ivecs");
    }

    private static void DownloadFile(string url, string path)
    {
        if (File.Exists(path)) return;
        var bytes = _http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        File.WriteAllBytes(path, bytes);
    }

    // ────────── fvecs / ivecs readers (ANN-benchmarks format) ──────────

    private static float[][] ReadFvecs(string path, int maxVectors)
    {
        var vectors = new List<float[]>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        while (fs.Position < fs.Length && vectors.Count < maxVectors)
        {
            int dim = br.ReadInt32();
            var v = new float[dim];
            var bytes = MemoryMarshal.AsBytes<float>(v).ToArray();
            br.Read(bytes);
            vectors.Add(v);
        }
        return vectors.ToArray();
    }

    private static float[][] ReadIvecsAsFloatGroundTruth(string path, int numQueries, int k)
    {
        var gt = new float[numQueries][];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        for (int qi = 0; qi < numQueries; qi++)
        {
            int len = br.ReadInt32();
            gt[qi] = new float[Math.Min(len, k)];
            for (int j = 0; j < gt[qi].Length; j++)
                gt[qi][j] = br.ReadInt32();
        }
        return gt;
    }
}
