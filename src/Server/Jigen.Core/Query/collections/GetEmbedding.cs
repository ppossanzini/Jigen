using Hikyaku;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Reads the stored full-precision embedding of a key. Null when the key does
/// not exist or the entry was stored without a vector.
/// </summary>
public class GetEmbedding : IRequest<float[]>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}
