using System.Runtime.InteropServices;

namespace Jigen.Persistance;

public partial class StoredList<T> : IList<T> where T : IStorableItem<T>
{
  [StructLayout(LayoutKind.Explicit)]
  public struct StoredListHeader
  {
    [FieldOffset(0)] public int Count;

    [FieldOffset(4)] public int TombStonedCount;

    [FieldOffset(8)] public long NextItemPosition;


    // No need of initialization, this array overlaps struct memory to allow fast serialization
    [FieldOffset(0)] public byte[] HeaderData;
    public const int HeaderSize = 16;
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct ItemIndex
  {
    [FieldOffset(0)] public long Position;
    [FieldOffset(8)] public int Length;
    [FieldOffset(12)] public ulong Hash;
    [FieldOffset(0)] public byte[] Data;

    public const int ItemIndexSize = 20;
  }
}