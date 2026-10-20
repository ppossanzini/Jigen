using Hikyaku;

namespace Jigen.Core.Query.metadata;

/// <summary>
/// Returns the shared metadata index associated to an embedding key, or -1
/// when the key exists without an association.
/// </summary>
public class GetMetadataIndex : IRequest<int>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}
