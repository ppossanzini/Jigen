using System.IO.MemoryMappedFiles;

namespace Jigen.Persistance;

internal sealed class MappedIndexFile : IDisposable
{
  private readonly FileStream _stream;
  private readonly int _viewSize;
  private readonly Lock _gate = new();
  private MemoryMappedFile _mapping;
  private MemoryMappedViewAccessor _view;
  private long _viewStart = -1;

  public MappedIndexFile(DiskIndexOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentException.ThrowIfNullOrWhiteSpace(options.FilePath);
    if (options.ViewSize < 65536 || options.ViewSize > 1024 * 1024
      || options.ViewSize % 65536 != 0)
      throw new ArgumentOutOfRangeException(nameof(options.ViewSize));

    _viewSize = options.ViewSize;
    _stream = new(options.FilePath, options.Recreate ? FileMode.Create : FileMode.OpenOrCreate,
      FileAccess.ReadWrite, FileShare.Read);
    try
    {
      if (_stream.Length > 0) Map();
    }
    catch
    {
      _stream.Dispose();
      throw;
    }
  }

  public long Length => _stream.Length;

  public void Grow(long length)
  {
    lock (_gate)
    {
      if (length <= Length) return;
      Flush();
      CloseMapping();
      _stream.SetLength(length);
      Map();
    }
  }

  public unsafe void Read(long offset, Span<byte> destination)
  {
    lock (_gate)
    {
      EnsureView(offset, destination.Length);
      byte* pointer = null;
      _view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
      try
      {
        new ReadOnlySpan<byte>(pointer + _view.PointerOffset + offset - _viewStart,
          destination.Length).CopyTo(destination);
      }
      finally
      {
        _view.SafeMemoryMappedViewHandle.ReleasePointer();
      }
    }
  }

  public unsafe void Write(long offset, ReadOnlySpan<byte> source)
  {
    lock (_gate)
    {
      EnsureView(offset, source.Length);
      byte* pointer = null;
      _view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
      try
      {
        source.CopyTo(new Span<byte>(pointer + _view.PointerOffset + offset - _viewStart,
          source.Length));
      }
      finally
      {
        _view.SafeMemoryMappedViewHandle.ReleasePointer();
      }
    }
  }

  public void Flush()
  {
    lock (_gate)
    {
      _view?.Flush();
      _stream.Flush(true);
    }
  }

  private void EnsureView(long offset, int length)
  {
    if (offset < 0 || length < 0 || offset > Length - length)
      throw new InvalidDataException("Mapped access is outside the file.");
    var start = offset / _viewSize * _viewSize;
    if (offset - start + length > _viewSize)
      throw new InvalidDataException("Mapped access crosses a view boundary.");
    if (_viewStart == start) return;
    _view?.Flush();
    _view?.Dispose();
    _view = null;
    _viewStart = -1;
    _view = _mapping.CreateViewAccessor(start, Math.Min(_viewSize, Length - start),
      MemoryMappedFileAccess.ReadWrite);
    _viewStart = start;
  }

  private void Map() => _mapping = MemoryMappedFile.CreateFromFile(_stream, null, 0,
    MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

  private void CloseMapping()
  {
    _view?.Dispose();
    _view = null;
    _viewStart = -1;
    _mapping?.Dispose();
    _mapping = null;
  }

  public void Dispose()
  {
    lock (_gate)
    {
      try { CloseMapping(); }
      finally { _stream.Dispose(); }
    }
  }
}