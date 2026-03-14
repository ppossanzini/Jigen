using System.Runtime.InteropServices;

namespace Jigen.Persistance;


  [StructLayout(LayoutKind.Explicit)]
  public struct StoredListHeader
  {
    [FieldOffset(0)] public int Count;

    [FieldOffset(4)] public int TombStonedCount;

    [FieldOffset(8)] public long NextItemPosition;
    
    public byte[] HeaderData => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1)).ToArray();
    public const int HeaderSize = 16;
  }

  [StructLayout(LayoutKind.Explicit)]
  public struct ItemIndex
  {
    [FieldOffset(0)] public long Position;
    [FieldOffset(8)] public int Length;
    [FieldOffset(12)] public ulong Hash;
    public byte[] Data => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1)).ToArray();

    public const int ItemIndexSize = 20;
  }
