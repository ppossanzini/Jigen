using Hikyaku;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Reads the stored full-precision vectors of a key, in storage order. Null
/// when the key does not exist or the entry was stored without vectors.
/// </summary>
public class GetEmbedding : IRequest<float[][]>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}
