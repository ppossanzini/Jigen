using Jigen.Persistance;
using MessagePack;

namespace Jigen.DataStructures;

[MessagePackObject(AllowPrivate = true)]
internal sealed record SharedMetadataAssociation(
  [property: Key(0)] string Collection,
  [property: Key(1)] byte[] EmbeddingId,
  [property: Key(2)] int MetadataIndex,
  [property: Key(3)] SharedMetadataMutationKind Kind = SharedMetadataMutationKind.Association,
  [property: Key(4)] int ContentIndex = 0) : IStorableItem<SharedMetadataAssociation, object>
{
  public ReadOnlyMemory<byte> Serialize() => MessagePackSerializer.Serialize(this);

  public static SharedMetadataAssociation Deserialize(ReadOnlyMemory<byte> data, object options)
    => MessagePackSerializer.Deserialize<SharedMetadataAssociation>(data);
}