using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jigen.Persistance;

[StructLayout(LayoutKind.Sequential)]
public struct StoredListHeader
{
  public int Count;
  public int TombStonedCount;
  public long NextItemPosition;
  public bool HasOwnHeader;
  public long HeaderPosition;
  public long HeaderSize;

  public static readonly int Size = Unsafe.SizeOf<StoredListHeader>();
}

[StructLayout(LayoutKind.Sequential)]
public struct ItemIndex
{
  public long Position;
  public int Length;
  public int MaxLength;
  public ulong Hash;

  public static readonly int Size = Unsafe.SizeOf<ItemIndex>();
}
