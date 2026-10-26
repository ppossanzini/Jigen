using System.Buffers.Binary;
using System.IO.Compression;

namespace Jigen.SemanticTools;

/// <summary>
/// The learned sparse head of BGE-M3 (<c>sparse_linear.pt</c> in the
/// <c>BAAI/bge-m3</c> repository): one linear layer from the token hidden state
/// to a single weight, <c>relu(w · h + b)</c>.
/// <para>
/// The file is read as PyTorch writes it, without PyTorch: a zip whose
/// <c>data/</c> entries are the raw little-endian storages of the state dict
/// (float32, float16 or bfloat16, as named in <c>data.pkl</c>; the published
/// file is float16). The layer has exactly two, so they are told apart by size:
/// the bias is one value, the weight is one value per hidden dimension.
/// </para>
/// </summary>
public sealed class LearnedSparseHead
{
  private readonly float[] _weight;
  private readonly float _bias;

  public LearnedSparseHead(float[] weight, float bias)
  {
    ArgumentNullException.ThrowIfNull(weight);
    if (weight.Length == 0)
      throw new ArgumentException("The sparse head weight cannot be empty.", nameof(weight));

    _weight = weight;
    _bias = bias;
  }

  /// <summary>Hidden size the head expects from the model.</summary>
  public int HiddenSize => _weight.Length;

  /// <summary>Weight of one token from its hidden state; zero means "not a term".</summary>
  public float Score(ReadOnlySpan<float> hidden)
  {
    var sum = _bias;
    for (var i = 0; i < _weight.Length; i++)
      sum += _weight[i] * hidden[i];

    return sum > 0f ? sum : 0f;
  }

  /// <summary>Loads the head from a PyTorch <c>state_dict</c> file (<c>sparse_linear.pt</c>).</summary>
  public static LearnedSparseHead Load(string path)
  {
    if (!File.Exists(path))
      throw new FileNotFoundException($"Learned sparse head not found at '{path}'.", path);

    using var archive = ZipFile.OpenRead(path);

    var byteOrder = archive.Entries.FirstOrDefault(entry => entry.Name == "byteorder");
    if (byteOrder != null)
    {
      using var reader = new StreamReader(byteOrder.Open());
      if (!string.Equals(reader.ReadToEnd().Trim(), "little", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException($"'{path}' is not little-endian.");
    }

    var storages = archive.Entries
      .Where(entry => entry.FullName.Contains("/data/", StringComparison.Ordinal) && entry.Length > 0)
      .ToArray();

    var type = ReadStorageType(archive);
    var elementSize = type == StorageType.Float ? sizeof(float) : sizeof(ushort);

    var bias = storages.Where(entry => entry.Length == elementSize).ToArray();
    var weight = storages.Where(entry => entry.Length > elementSize).ToArray();
    if (storages.Length != 2 || bias.Length != 1 || weight.Length != 1 || weight[0].Length % elementSize != 0)
      throw new InvalidDataException(
        $"'{path}' is not a sparse head: expected one weight and one single-value bias storage.");

    return new LearnedSparseHead(ReadValues(weight[0], type), ReadValues(bias[0], type)[0]);
  }

  private enum StorageType { Float, Half, BFloat16 }

  // The pickled state dict names the storage class of its tensors
  // (the published BGE-M3 head is HalfStorage).
  private static StorageType ReadStorageType(ZipArchive archive)
  {
    var pickle = archive.Entries.FirstOrDefault(entry => entry.Name == "data.pkl");
    if (pickle == null)
      return StorageType.Float;

    using var reader = new StreamReader(pickle.Open(), System.Text.Encoding.Latin1);
    var text = reader.ReadToEnd();
    if (text.Contains("BFloat16Storage", StringComparison.Ordinal))
      return StorageType.BFloat16;
    return text.Contains("HalfStorage", StringComparison.Ordinal) ? StorageType.Half : StorageType.Float;
  }

  private static float[] ReadValues(ZipArchiveEntry entry, StorageType type)
  {
    var bytes = new byte[entry.Length];
    using (var stream = entry.Open())
      stream.ReadExactly(bytes);

    if (type == StorageType.Float)
    {
      var floats = new float[bytes.Length / sizeof(float)];
      for (var i = 0; i < floats.Length; i++)
        floats[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
      return floats;
    }

    var values = new float[bytes.Length / sizeof(ushort)];
    for (var i = 0; i < values.Length; i++)
    {
      var raw = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * sizeof(ushort)));
      values[i] = type == StorageType.Half
        ? (float)BitConverter.UInt16BitsToHalf(raw)
        : BitConverter.Int32BitsToSingle(raw << 16);
    }

    return values;
  }
}
