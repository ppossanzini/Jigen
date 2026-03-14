namespace Jigen.Persistance;

public interface IStorableItem<T>
{
  ReadOnlyMemory<byte> Serialize();
  static abstract T Deserialize(ReadOnlyMemory<byte> data);
}