using System.Globalization;

namespace Jigen.Index.Sparse;

/// <summary>
/// Lists the live segments of a collection. The file is replaced atomically, so
/// a reader never sees a half-written list and a segment becomes visible only
/// once its files are complete.
/// </summary>
public static class SparseCollectionManifest
{
    private const string FileName = "manifest";

    public static IReadOnlyList<long> Read(string directoryPath)
    {
        var path = Path.Combine(directoryPath, FileName);
        if (!File.Exists(path))
            return [];

        var generations = new List<long>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!long.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation) ||
                generation < 1)
                throw new InvalidDataException($"Invalid segment generation in '{path}'.");
            generations.Add(generation);
        }

        generations.Sort();
        return generations;
    }

    public static void Write(string directoryPath, IEnumerable<long> generations)
    {
        var path = Path.Combine(directoryPath, FileName);
        var temporary = path + ".tmp";
        File.WriteAllLines(temporary,
            generations.Order().Select(generation => generation.ToString(CultureInfo.InvariantCulture)));
        File.Move(temporary, path, overwrite: true);
    }
}
