using System.Buffers.Binary;
using System.IO.Hashing;

namespace Jigen.Persistance;

public sealed class PersistentScalarArray : IDisposable
{
  private const int PageSize = 4096;
  private const int SlotSize = 32;
  private const int SlotsPerBlock = 128;
  private const int HeaderSize = PageSize * 2;
  private const ulong Magic = 0x315952524147494A;
  private readonly MappedIndexFile _file;
  private readonly ReaderWriterLockSlim _gate = new();
  private long _capacity;
  private long _headerGeneration;
  private int _headerCopy;
  private bool _failed;
  private bool _disposed;

  public PersistentScalarArray(DiskIndexOptions options)
  {
    _file = new(options);
    try
    {
      if (_file.Length == 0)
      {
        _file.Grow(HeaderSize);
        _headerGeneration = 1;
        WriteHeader(0, 1, 0);
        _file.Flush();
        WriteHeader(1, 1, 0);
        _file.Flush();
      }
      else
      {
        ReadHeader();
      }
    }
    catch
    {
      _file.Dispose();
      throw;
    }
  }

  public bool TryGet(long id, out long value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(id);
    _gate.EnterReadLock();
    try
    {
      EnsureAvailable();
      value = 0;
      if (id >= _capacity) return false;
      Span<byte> slot = stackalloc byte[SlotSize];
      ReadSlot(id, slot);
      if (BinaryPrimitives.ReadInt32LittleEndian(slot[24..]) != 1) return false;
      value = BinaryPrimitives.ReadInt64LittleEndian(slot[16..]);
      return true;
    }
    finally { _gate.ExitReadLock(); }
  }

  public void Set(long id, long value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(id);
    var capacity = checked((id / SlotsPerBlock + 1) * SlotsPerBlock);
    _ = RequiredLength(capacity);
    _gate.EnterWriteLock();
    try
    {
      EnsureAvailable();
      try
      {
        if (capacity > _capacity) Grow(capacity);
        WriteSlot(id, value, 1);
      }
      catch
      {
        _failed = true;
        throw;
      }
    }
    finally { _gate.ExitWriteLock(); }
  }

  public bool Delete(long id)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(id);
    _gate.EnterWriteLock();
    try
    {
      EnsureAvailable();
      if (id >= _capacity) return false;
      try
      {
        Span<byte> slot = stackalloc byte[SlotSize];
        ReadSlot(id, slot);
        if (BinaryPrimitives.ReadInt32LittleEndian(slot[24..]) != 1) return false;
        WriteSlot(id, 0, 2);
        return true;
      }
      catch
      {
        _failed = true;
        throw;
      }
    }
    finally { _gate.ExitWriteLock(); }
  }

  private void Grow(long capacity)
  {
    _file.Grow(RequiredLength(capacity));
    var block = new byte[PageSize * 2];
    for (var firstId = _capacity; firstId < capacity; firstId += SlotsPerBlock)
    {
      for (var index = 0; index < SlotsPerBlock; index++)
      {
        var slot = block.AsSpan(index * SlotSize, SlotSize);
        EncodeSlot(slot, firstId + index, 1, 0, 0);
        slot.CopyTo(block.AsSpan(PageSize + index * SlotSize, SlotSize));
      }
      _file.Write(SlotOffset(firstId, 0), block);
    }
    _file.Flush();
    var generation = checked(_headerGeneration + 1);
    var copy = 1 - _headerCopy;
    WriteHeader(copy, generation, capacity);
    _file.Flush();
    _capacity = capacity;
    _headerGeneration = generation;
    _headerCopy = copy;
  }

  private void WriteSlot(long id, long value, int state)
  {
    Span<byte> slot = stackalloc byte[SlotSize];
    var activeCopy = ReadSlot(id, slot);
    var generation = checked(BinaryPrimitives.ReadInt64LittleEndian(slot[8..]) + 1);
    EncodeSlot(slot, id, generation, value, state);
    _file.Write(SlotOffset(id, 1 - activeCopy), slot);
    _file.Flush();
  }

  private int ReadSlot(long id, Span<byte> result)
  {
    Span<byte> other = stackalloc byte[SlotSize];
    _file.Read(SlotOffset(id, 0), result);
    _file.Read(SlotOffset(id, 1), other);
    var firstValid = ValidSlot(result, id);
    var secondValid = ValidSlot(other, id);
    if (!firstValid && !secondValid)
      throw new InvalidDataException("Both slot copies are invalid.");
    var firstGeneration = BinaryPrimitives.ReadInt64LittleEndian(result[8..]);
    var secondGeneration = BinaryPrimitives.ReadInt64LittleEndian(other[8..]);
    if (firstValid && secondValid && firstGeneration == secondGeneration
      && !result.SequenceEqual(other))
      throw new InvalidDataException("Slot copies disagree at the same generation.");
    if (secondValid && (!firstValid || secondGeneration > firstGeneration))
    {
      other.CopyTo(result);
      return 1;
    }
    return 0;
  }

  private static bool ValidSlot(ReadOnlySpan<byte> slot, long id) =>
    BinaryPrimitives.ReadInt64LittleEndian(slot) == id
    && BinaryPrimitives.ReadInt64LittleEndian(slot[8..]) > 0
    && BinaryPrimitives.ReadInt32LittleEndian(slot[24..]) is >= 0 and <= 2
    && BinaryPrimitives.ReadUInt32LittleEndian(slot[28..]) == Crc32.HashToUInt32(slot[..28]);

  private static void EncodeSlot(Span<byte> slot, long id, long generation, long value, int state)
  {
    BinaryPrimitives.WriteInt64LittleEndian(slot, id);
    BinaryPrimitives.WriteInt64LittleEndian(slot[8..], generation);
    BinaryPrimitives.WriteInt64LittleEndian(slot[16..], value);
    BinaryPrimitives.WriteInt32LittleEndian(slot[24..], state);
    BinaryPrimitives.WriteUInt32LittleEndian(slot[28..], Crc32.HashToUInt32(slot[..28]));
  }

  private void WriteHeader(int copy, long generation, long capacity)
  {
    var page = new byte[PageSize];
    BinaryPrimitives.WriteUInt64LittleEndian(page, Magic);
    BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(8), 1);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(16), generation);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(24), capacity);
    BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(PageSize - 4),
      Crc32.HashToUInt32(page.AsSpan(0, PageSize - 4)));
    _file.Write((long)copy * PageSize, page);
  }

  private void ReadHeader()
  {
    if (_file.Length < HeaderSize)
      throw new InvalidDataException("Array header is incomplete.");
    var page = new byte[PageSize];
    for (var copy = 0; copy < 2; copy++)
    {
      _file.Read((long)copy * PageSize, page);
      if (BinaryPrimitives.ReadUInt64LittleEndian(page) != Magic
        || BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(8)) != 1
        || BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(PageSize - 4))
          != Crc32.HashToUInt32(page.AsSpan(0, PageSize - 4))) continue;
      var generation = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(16));
      var capacity = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(24));
      if (generation <= 0 || capacity < 0 || capacity % SlotsPerBlock != 0
        || capacity > (_file.Length - HeaderSize) / (PageSize * 2) * SlotsPerBlock) continue;
      if (generation == _headerGeneration && capacity != _capacity)
        throw new InvalidDataException("Array headers disagree at the same generation.");
      if (generation <= _headerGeneration) continue;
      _headerGeneration = generation;
      _capacity = capacity;
      _headerCopy = copy;
    }
    if (_headerGeneration == 0)
      throw new InvalidDataException("No valid array header was recovered.");
  }

  private static long RequiredLength(long capacity) =>
    checked(HeaderSize + capacity / SlotsPerBlock * (PageSize * 2));

  private static long SlotOffset(long id, int copy) =>
    checked(HeaderSize + id / SlotsPerBlock * (PageSize * 2)
      + copy * PageSize + id % SlotsPerBlock * SlotSize);

  private void EnsureAvailable()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if (_failed) throw new InvalidOperationException("Reopen the array after a failed mutation.");
  }

  public void Dispose()
  {
    _gate.EnterWriteLock();
    try
    {
      if (_disposed) return;
      _disposed = true;
      _file.Dispose();
    }
    finally { _gate.ExitWriteLock(); }
  }
}