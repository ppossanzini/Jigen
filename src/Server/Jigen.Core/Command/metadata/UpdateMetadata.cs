using Hikyaku;

namespace Jigen.Core.Command.metadata;

/// <summary>
/// Replaces the content of a shared metadata record. Every embedding
/// associated to the record reads the new content. Returns false when the
/// metadata index does not exist.
/// </summary>
public class UpdateMetadata : IRequest<bool>
{
  public string Database { get; set; }
  public int MetadataIndex { get; set; }
  public byte[] Content { get; set; }
}
