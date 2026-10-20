using Hikyaku;

namespace Jigen.Core.Command.metadata;

/// <summary>
/// Creates a shared metadata record without associating it to an embedding.
/// The returned index is the opaque metadata identity: stable across reopen
/// and compaction, never reused after retirement.
/// </summary>
public class CreateMetadata : IRequest<int>
{
  public string Database { get; set; }
  public byte[] Content { get; set; }
}
