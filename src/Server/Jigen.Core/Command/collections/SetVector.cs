using Jigen.DataStructures;
using Hikyaku;

namespace Jigen.Core.Command.collections;

public class SetVector: IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float[] Embeddings { get; set; }

  /// <summary>
  /// Lexical slot: term ids with their weights, supplied by the caller. Null
  /// leaves the entry without one; it is never derived from the content.
  /// </summary>
  public SparseVector Sparse { get; set; }
}
