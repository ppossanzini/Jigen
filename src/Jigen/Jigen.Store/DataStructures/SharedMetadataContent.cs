using Jigen.Persistance;

namespace Jigen.DataStructures;

internal sealed class SharedMetadataContent(byte[] content) : IStorableItem<SharedMetadataContent, object>
{
  public byte[] Content { get; } = content;

  public ReadOnlyMemory<byte> Serialize() => Content;

  public static SharedMetadataContent Deserialize(ReadOnlyMemory<byte> data, object options)
    => new(data.ToArray());
}