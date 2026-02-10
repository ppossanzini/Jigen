using System;
using System.Collections.Generic;
using System.IO.Hashing;

namespace Jigen;

public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
{
  public static readonly ByteArrayEqualityComparer Instance = new();

  public bool Equals(byte[]? x, byte[]? y)
  {
    if (ReferenceEquals(x, y)) return true;
    if (x is null || y is null) return false;
    if (x.Length != y.Length) return false;

    return x.AsSpan().SequenceEqual(y);
  }

  public int GetHashCode(byte[] obj)
  {
    if (obj is null) throw new ArgumentNullException(nameof(obj));

    ReadOnlySpan<byte> span = obj;
    // Molto veloce e con bassa probabilità di collisione.
    ulong h64 = XxHash3.HashToUInt64(span);

    // Mescola 64->32 (xor-fold) + cast
    return unchecked((int)(h64 ^ (h64 >> 32)));
  }
}