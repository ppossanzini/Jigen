using System.Runtime.InteropServices;

namespace Jigen.Persistance;

  public struct StoredListHeader
  {
     public int Count;

     public int TombStonedCount;

     public long NextItemPosition;
    
    public byte[] HeaderData => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1)).ToArray();
    
  }

  public struct ItemIndex
  {
     public long Position;
     public int Length;
     public ulong Hash;
    public byte[] Data => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1)).ToArray();
    
  }
