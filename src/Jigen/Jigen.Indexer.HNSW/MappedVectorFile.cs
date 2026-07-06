using System.IO.MemoryMappedFiles;

namespace Jigen.Indexer;

/// <summary>
/// Read-only memory mapping of the graph's vector file: distances are
/// computed on spans over the mapped pages — no deserialization, no float[]
/// allocation, no read syscalls on the hot path.
///
/// The file is append-only, so offsets are stable forever. When it outgrows
/// the mapping, a new full-length view is published atomically and the old
/// one is retired but NOT disposed until Dispose: spans handed out from it
/// stay valid, and both views read the same page-cache pages.
/// </summary>
internal sealed unsafe class MappedVectorFile : IDisposable
{
  private sealed class View
  {
    public MemoryMappedFile File;
    public MemoryMappedViewAccessor Accessor;
    public byte* Pointer;
    public long Length;
  }

  private readonly string _path;
  private readonly List<View> _retired = new();
  private View _current;
  private bool _disposed;

  public MappedVectorFile(string path)
  {
    _path = path;
    Remap();
  }

  /// <summary>Bytes covered by the current mapping.</summary>
  public long MappedLength => Volatile.Read(ref _current)?.Length ?? 0;

  /// <summary>
  /// Re-maps the file at its current length. Callers must guarantee mutual
  /// exclusion (the graph lock); readers keep working on the old view.
  /// </summary>
  public void Remap()
  {
    if (_disposed) return;

    var length = File.Exists(_path) ? new FileInfo(_path).Length : 0;
    if (length <= 0) return; // nothing to map yet (file not created or empty)

    var current = _current;
    if (current is not null && current.Length >= length) return;

    var file = MemoryMappedFile.CreateFromFile(
      File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
      null, length, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
    var accessor = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);

    byte* pointer = null;
    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

    if (current is not null) _retired.Add(current);
    Volatile.Write(ref _current, new View { File = file, Accessor = accessor, Pointer = pointer, Length = length });
  }

  /// <summary>Floats at an absolute byte offset. The caller guarantees the
  /// mapping covers the range (vectors are only served from the map once a
  /// remap has covered their offset).</summary>
  public ReadOnlySpan<float> Floats(long byteOffset, int count)
  {
    var view = Volatile.Read(ref _current);
    return new ReadOnlySpan<float>(view.Pointer + byteOffset, count);
  }

  /// <summary>Raw bytes at an absolute offset (record headers, ids).</summary>
  public ReadOnlySpan<byte> Bytes(long byteOffset, int count)
  {
    var view = Volatile.Read(ref _current);
    return new ReadOnlySpan<byte>(view.Pointer + byteOffset, count);
  }

  /// <summary>Drops every view. Only safe when no reader can still hold spans
  /// (the owning store is being closed).</summary>
  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;

    if (_current is not null) _retired.Add(_current);
    _current = null;

    foreach (var view in _retired)
    {
      view.Accessor.SafeMemoryMappedViewHandle.ReleasePointer();
      view.Accessor.Dispose();
      view.File.Dispose();
    }

    _retired.Clear();
  }
}
