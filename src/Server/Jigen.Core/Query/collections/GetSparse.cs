using Hikyaku;
using Jigen.DataStructures;

namespace Jigen.Core.Query.collections;

/// <summary>
/// Reads the stored sparse vector of a key. Null when the key does not exist
/// or the entry was stored without a lexical slot.
/// </summary>
public class GetSparse : IRequest<SparseVector>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
}
