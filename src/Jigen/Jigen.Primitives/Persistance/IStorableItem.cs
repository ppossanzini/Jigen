namespace Jigen.Persistance;

public interface IStorableItem<T>
{
  ReadOnlySpan<byte> StoredKey { get; set; }
  ReadOnlySpan<byte> Serialize();
  static abstract T Deserialize(ReadOnlySpan<byte> data);
}