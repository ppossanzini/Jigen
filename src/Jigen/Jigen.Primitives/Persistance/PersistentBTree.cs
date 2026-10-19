using System.Buffers.Binary;
using System.IO.Hashing;
using TreeEntry = (byte[] Key, long Value, int State);
using TreeNode = (int Level, System.Collections.Generic.List<(byte[] Key, long Value, int State)> Entries);

namespace Jigen.Persistance;

public sealed class PersistentBTree : IDisposable
{
  private const int PageSize = 4096;
  private const int PageHeaderSize = 64;
  private const int FileHeaderSize = PageSize * 2;
  private const int MaxKeyLength = 512;
  private const int MaxLevel = 31;
  private const ulong RootMagic = 0x3152544247494A;
  private const ulong PageMagic = 0x3147504247494A;
  private readonly MappedIndexFile _file;
  private readonly ReaderWriterLockSlim _gate = new();
  private long _root;
  private long _end;
  private long _generation;
  private long _nextOffset;
  private int _rootCopy;
  private bool _failed;
  private bool _disposed;

  public PersistentBTree(DiskIndexOptions options)
  {
    _file = new(options);
    try
    {
      if (_file.Length == 0)
      {
        _file.Grow(FileHeaderSize);
        _nextOffset = FileHeaderSize;
        _root = AppendPage((0, []), 1);
        _file.Flush();
        _end = _nextOffset;
        WriteRoot(0, 1, _root, _end);
        _file.Flush();
        WriteRoot(1, 1, _root, _end);
        _file.Flush();
        _generation = 1;
      }
      else
      {
        ReadRoot();
      }
    }
    catch
    {
      _file.Dispose();
      throw;
    }
  }

  public bool TryGet(ReadOnlySpan<byte> key, out long value)
  {
    ValidateKey(key);
    _gate.EnterReadLock();
    try
    {
      EnsureAvailable();
      return Lookup(key, out value);
    }
    finally { _gate.ExitReadLock(); }
  }

  public void Set(ReadOnlySpan<byte> key, long value)
  {
    ValidateKey(key);
    _gate.EnterWriteLock();
    try
    {
      EnsureAvailable();
      Commit(key, value, 1);
    }
    finally { _gate.ExitWriteLock(); }
  }

  public bool Delete(ReadOnlySpan<byte> key)
  {
    ValidateKey(key);
    _gate.EnterWriteLock();
    try
    {
      EnsureAvailable();
      if (!Lookup(key, out _)) return false;
      Commit(key, 0, 2);
      return true;
    }
    finally { _gate.ExitWriteLock(); }
  }

  private bool Lookup(ReadOnlySpan<byte> key, out long value)
  {
    var offset = _root;
    var expectedLevel = -1;
    while (true)
    {
      var node = ReadPage(offset, _end, _generation);
      if (expectedLevel >= 0 && node.Level != expectedLevel)
        throw new InvalidDataException("Tree child level is inconsistent.");
      var index = LowerBound(node.Entries, key);
      if (node.Level == 0)
      {
        value = 0;
        if (index == node.Entries.Count || !key.SequenceEqual(node.Entries[index].Key)
          || node.Entries[index].State != 1) return false;
        value = node.Entries[index].Value;
        return true;
      }
      index = ChildIndex(node.Entries, key, index);
      offset = node.Entries[index].Value;
      expectedLevel = node.Level - 1;
    }
  }

  private void Commit(ReadOnlySpan<byte> key, long value, int state)
  {
    try
    {
      var generation = checked(_generation + 1);
      _nextOffset = checked((_file.Length + PageSize - 1) / PageSize * PageSize);
      var rootNode = ReadPage(_root, _end, _generation);
      var updated = Update(_root, rootNode.Level, key, value, state, generation);
      var newRoot = updated.Left;
      if (updated.Right != 0)
      {
        if (rootNode.Level == MaxLevel)
          throw new InvalidDataException("Tree exceeds the supported addressable depth.");
        List<TreeEntry> children =
          [(updated.LeftKey, updated.Left, 0), (updated.RightKey, updated.Right, 0)];
        newRoot = AppendPage((rootNode.Level + 1, children), generation);
      }
      _file.Flush();
      var copy = 1 - _rootCopy;
      WriteRoot(copy, generation, newRoot, _nextOffset);
      _file.Flush();
      _root = newRoot;
      _end = _nextOffset;
      _generation = generation;
      _rootCopy = copy;
    }
    catch
    {
      _failed = true;
      throw;
    }
  }

  private (long Left, byte[] LeftKey, long Right, byte[] RightKey) Update(
    long offset, int expectedLevel, ReadOnlySpan<byte> key, long value, int state, long generation)
  {
    var node = ReadPage(offset, _end, _generation);
    if (node.Level != expectedLevel)
      throw new InvalidDataException("Tree child level is inconsistent.");
    var index = LowerBound(node.Entries, key);
    if (node.Level == 0)
    {
      var entry = (key.ToArray(), value, state);
      if (index < node.Entries.Count && key.SequenceEqual(node.Entries[index].Key))
        node.Entries[index] = entry;
      else
        node.Entries.Insert(index, entry);
    }
    else
    {
      index = ChildIndex(node.Entries, key, index);
      var updated = Update(node.Entries[index].Value, node.Level - 1,
        key, value, state, generation);
      node.Entries[index] = (updated.LeftKey, updated.Left, 0);
      if (updated.Right != 0)
        node.Entries.Insert(index + 1, (updated.RightKey, updated.Right, 0));
    }
    if (SerializedSize(node.Entries) <= PageSize)
      return (AppendPage(node, generation), node.Entries[0].Key, 0, null);

    var split = FindSplit(node.Entries);
    var rightEntries = node.Entries.GetRange(split, node.Entries.Count - split);
    node.Entries.RemoveRange(split, node.Entries.Count - split);
    var left = AppendPage(node, generation);
    var right = AppendPage((node.Level, rightEntries), generation);
    return (left, node.Entries[0].Key, right, rightEntries[0].Key);
  }

  private static int FindSplit(List<TreeEntry> entries)
  {
    var target = (SerializedSize(entries) - PageHeaderSize) / 2;
    var bytes = 0;
    var split = 1;
    for (var index = 0; index < entries.Count - 1; index++)
    {
      bytes += entries[index].Key.Length + 16;
      split = index + 1;
      if (bytes >= target) break;
    }
    if (SerializedSize(entries.GetRange(0, split)) > PageSize
      || SerializedSize(entries.GetRange(split, entries.Count - split)) > PageSize)
      throw new InvalidDataException("Tree split does not fit its pages.");
    return split;
  }

  private long AppendPage(TreeNode node, long generation)
  {
    var offset = _nextOffset;
    var end = checked(offset + PageSize);
    var page = new byte[PageSize];
    BinaryPrimitives.WriteUInt64LittleEndian(page, PageMagic);
    BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(8), node.Level);
    BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(12), node.Entries.Count);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(16), offset);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(24), generation);
    var payload = PageHeaderSize + node.Entries.Count * 4;
    for (var index = 0; index < node.Entries.Count; index++)
    {
      var entry = node.Entries[index];
      var directory = page.AsSpan(PageHeaderSize + index * 4, 4);
      BinaryPrimitives.WriteUInt16LittleEndian(directory, checked((ushort)payload));
      BinaryPrimitives.WriteUInt16LittleEndian(directory[2..], checked((ushort)entry.Key.Length));
      entry.Key.CopyTo(page.AsSpan(payload));
      payload += entry.Key.Length;
      BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(payload), entry.Value);
      BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(payload + 8), entry.State);
      payload += 12;
    }
    BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(60), Crc32.HashToUInt32(page));
    _file.Grow(end);
    _file.Write(offset, page);
    _nextOffset = end;
    return offset;
  }

  private TreeNode ReadPage(long offset, long end, long generation)
  {
    if (offset < FileHeaderSize || offset % PageSize != 0 || offset > end - PageSize)
      throw new InvalidDataException("Tree page address is outside the committed range.");
    var page = new byte[PageSize];
    _file.Read(offset, page);
    var checksum = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(60));
    BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(60), 0);
    var level = BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(8));
    var count = BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(12));
    var pageGeneration = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(24));
    if (BinaryPrimitives.ReadUInt64LittleEndian(page) != PageMagic
      || BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(16)) != offset
      || checksum != Crc32.HashToUInt32(page) || level < 0 || level > MaxLevel
      || pageGeneration <= 0 || pageGeneration > generation
      || count < 0 || count > (PageSize - PageHeaderSize) / 17
      || (level > 0 && count < 2))
      throw new InvalidDataException("Tree page header is invalid.");
    List<TreeEntry> entries = new(count);
    var expectedPayload = PageHeaderSize + count * 4;
    for (var index = 0; index < count; index++)
    {
      var directory = page.AsSpan(PageHeaderSize + index * 4, 4);
      var payload = BinaryPrimitives.ReadUInt16LittleEndian(directory);
      var length = BinaryPrimitives.ReadUInt16LittleEndian(directory[2..]);
      if (payload != expectedPayload || length is 0 or > MaxKeyLength
        || payload + length + 12 > PageSize)
        throw new InvalidDataException("Tree page slot is invalid.");
      var key = page.AsSpan(payload, length).ToArray();
      var value = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(payload + length));
      var state = BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(payload + length + 8));
      if ((index > 0 && entries[index - 1].Key.AsSpan().SequenceCompareTo(key) >= 0)
        || (level == 0 && state is not (1 or 2))
        || (level > 0 && (state != 0 || value < FileHeaderSize || value >= offset
          || value % PageSize != 0)))
        throw new InvalidDataException("Tree page entry is invalid.");
      entries.Add((key, value, state));
      expectedPayload += length + 12;
    }
    return (level, entries);
  }

  private void WriteRoot(int copy, long generation, long root, long end)
  {
    var page = new byte[PageSize];
    BinaryPrimitives.WriteUInt64LittleEndian(page, RootMagic);
    BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(8), 1);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(16), generation);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(24), root);
    BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(32), end);
    BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(PageSize - 4),
      Crc32.HashToUInt32(page.AsSpan(0, PageSize - 4)));
    _file.Write((long)copy * PageSize, page);
  }

  private void ReadRoot()
  {
    if (_file.Length < FileHeaderSize)
      throw new InvalidDataException("Tree root records are incomplete.");
    var page = new byte[PageSize];
    for (var copy = 0; copy < 2; copy++)
    {
      _file.Read((long)copy * PageSize, page);
      if (BinaryPrimitives.ReadUInt64LittleEndian(page) != RootMagic
        || BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(8)) != 1
        || BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(PageSize - 4))
          != Crc32.HashToUInt32(page.AsSpan(0, PageSize - 4))) continue;
      var generation = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(16));
      var root = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(24));
      var end = BinaryPrimitives.ReadInt64LittleEndian(page.AsSpan(32));
      if (generation <= 0 || end < FileHeaderSize + PageSize
        || end > _file.Length || end % PageSize != 0) continue;
      try { ReadPage(root, end, generation); }
      catch (InvalidDataException) { continue; }
      if (generation == _generation && (root != _root || end != _end))
        throw new InvalidDataException("Tree root records disagree at the same generation.");
      if (generation <= _generation) continue;
      _root = root;
      _end = end;
      _generation = generation;
      _rootCopy = copy;
    }
    if (_generation == 0)
      throw new InvalidDataException("No valid tree root was recovered.");
  }

  private static int SerializedSize(List<TreeEntry> entries)
  {
    var size = PageHeaderSize;
    foreach (var entry in entries) size += entry.Key.Length + 16;
    return size;
  }

  private static int LowerBound(List<TreeEntry> entries, ReadOnlySpan<byte> key)
  {
    var left = 0;
    var right = entries.Count;
    while (left < right)
    {
      var middle = left + (right - left) / 2;
      if (entries[middle].Key.AsSpan().SequenceCompareTo(key) < 0) left = middle + 1;
      else right = middle;
    }
    return left;
  }

  private static int ChildIndex(List<TreeEntry> entries, ReadOnlySpan<byte> key, int lowerBound) =>
    lowerBound < entries.Count && key.SequenceEqual(entries[lowerBound].Key)
      ? lowerBound : Math.Max(0, lowerBound - 1);

  private static void ValidateKey(ReadOnlySpan<byte> key)
  {
    if (key.Length is 0 or > MaxKeyLength)
      throw new ArgumentOutOfRangeException(nameof(key), "Key length must be between 1 and 512 bytes.");
  }

  private void EnsureAvailable()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if (_failed) throw new InvalidOperationException("Reopen the tree after a failed mutation.");
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