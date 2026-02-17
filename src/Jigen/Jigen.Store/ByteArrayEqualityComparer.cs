using System;
using System.Collections.Generic;
using System.IO.Hashing;

namespace Jigen;

public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
{
  public static readonly ByteArrayEqualityComparer Instance = new();

  public bool Equals(byte[] x, byte[] y)
  {
    if (ReferenceEquals(x, y)) return true;
    if (x is null || y is null) return false;
    return x.Length == y.Length && x.AsSpan().SequenceEqual(y);
  }

  public int GetHashCode(byte[] obj)
  {
    if (obj is null) throw new ArgumentNullException(nameof(obj));
    
    ReadOnlySpan<byte> span = obj;
    var h64 = XxHash3.HashToUInt64(span);
    return unchecked((int)(h64 ^ (h64 >> 32)));

  }
}