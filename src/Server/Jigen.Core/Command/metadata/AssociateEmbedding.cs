using Hikyaku;

namespace Jigen.Core.Command.metadata;

/// <summary>
/// Associates an existing embedding key to a shared metadata record, moving
/// it when the key was already associated to another record.
/// </summary>
public class AssociateEmbedding : IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public int MetadataIndex { get; set; }
}
