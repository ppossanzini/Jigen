namespace Jigen.Persistance;

public interface IStorableItem<T, TOptions> where T : IStorableItem<T, TOptions>
{
  ReadOnlyMemory<byte> Serialize();
  static abstract T Deserialize(ReadOnlyMemory<byte> data, TOptions options);
}
