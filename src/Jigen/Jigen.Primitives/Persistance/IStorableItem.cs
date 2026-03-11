namespace Jigen.Persistance;

public interface IStorableItem
{
  ReadOnlySpan<byte> Serialize();
  static abstract T Deserialize<T>(ReadOnlySpan<byte> data);
}