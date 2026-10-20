using Jigen.DataStructures;
using Jigen.Persistance;
using System.Buffers.Binary;
using System.Text;

namespace Jigen;

public sealed class SharedMetadataStore : IAsyncDisposable
{
  private StoredList<SharedMetadataContent, object> _contents;
  private StoredList<SharedMetadataAssociation, object> _associations;
  private PersistentBTree _associationIndex;
  private PersistentScalarArray _contentIndex;
  private PersistentFileGeneration _generations;
  private readonly FileGenerationOptions _generationOptions;
  private readonly string _directory;
  private readonly string _manifestName;
  private readonly TimeSpan? _contentFlushInterval;
  private readonly TimeSpan? _associationFlushInterval;
  private string _contentPath;
  private string _associationPath;
  private readonly Lock _gate = new();
  private static readonly UTF8Encoding KeyEncoding = new(false, true);
  private bool _disposed;
  private bool _failed;

  public SharedMetadataStore(StoreListOptions contentOptions, StoreListOptions associationOptions)
  {
    if (Path.GetFullPath(contentOptions.FilePath) == Path.GetFullPath(associationOptions.FilePath))
      throw new ArgumentException("Content and associations require separate list paths.", nameof(associationOptions));

    _contentPath = Path.GetFullPath(contentOptions.FilePath);
    _associationPath = Path.GetFullPath(associationOptions.FilePath);
    _directory = Path.GetDirectoryName(_associationPath);
    _manifestName = Path.GetFileName(_associationPath) + ".generation";
    _contentFlushInterval = contentOptions.FlushInterval;
    _associationFlushInterval = associationOptions.FlushInterval;
    _generationOptions = new() { DirectoryPath = _directory, ManifestName = _manifestName };
    try
    {
      if (PersistentFileGeneration.HasActiveGeneration(_generationOptions))
      {
        _generations = new(_generationOptions);
        var active = _generations.GetActiveFiles();
        _contentPath = active["contents"];
        _associationPath = active["associations"];
      }
      OpenSources();
    }
    catch
    {
      DisposeAsync().AsTask().GetAwaiter().GetResult();
      throw;
    }
  }

  private void OpenSources()
  {
      _contents = new(new StoreListOptions { FilePath = _contentPath, FlushInterval = _contentFlushInterval }, null);
      _associations = new(new StoreListOptions { FilePath = _associationPath, FlushInterval = _associationFlushInterval }, null);
      _associationIndex = new(new DiskIndexOptions
      {
        FilePath = _associationPath + ".lookup", Recreate = true
      });
      _contentIndex = new(new DiskIndexOptions
      {
        FilePath = _contentPath + ".versions", Recreate = true
      });
      for (var index = 0; index < _contents.Count; index++) _contentIndex.Set(index, index);
      for (var index = 0; index < _associations.Count; index++)
      {
        var mutation = _associations[index];
        if (!_contentIndex.TryGet(mutation.MetadataIndex, out _))
          throw new InvalidDataException("Association references an unavailable metadata ID.");
        switch (mutation.Kind)
        {
          case SharedMetadataMutationKind.Association:
            _associationIndex.Set(EncodeKey(mutation.Collection, mutation.EmbeddingId), index);
            break;
          case SharedMetadataMutationKind.EmbeddingDeleted:
            _associationIndex.Delete(EncodeKey(mutation.Collection, mutation.EmbeddingId));
            break;
          case SharedMetadataMutationKind.MetadataUpdated:
            if (mutation.ContentIndex < 0 || mutation.ContentIndex >= _contents.Count
              || mutation.ContentIndex <= mutation.MetadataIndex)
              throw new InvalidDataException("Metadata update references an invalid content record.");
            _contentIndex.Delete(mutation.ContentIndex);
            _contentIndex.Set(mutation.MetadataIndex, mutation.ContentIndex);
            break;
          case SharedMetadataMutationKind.MetadataRetired:
            _contentIndex.Delete(mutation.MetadataIndex);
            break;
          default:
            throw new InvalidDataException("Unknown metadata mutation kind.");
        }
      }
  }

  public IReadOnlyList<string> GetFileNames()
  {
    lock (_gate)
    {
      EnsureAvailable();
      var files = new List<string>
      {
        _contentPath, _contentPath + ".index", _associationPath, _associationPath + ".index",
        _contentPath + ".versions", _associationPath + ".lookup"
      };
      if (_generations?.ActiveGeneration is not null)
        files.Add(Path.Combine(_directory, _manifestName + ".json"));
      return files;
    }
  }

  public long ContentSize
  {
    get { lock (_gate) { EnsureAvailable(); return new FileInfo(_contentPath).Length; } }
  }

  public long StorageSize => GetFileNames().Sum(path => new FileInfo(path).Length);

  public long DataSize
  {
    get
    {
      lock (_gate)
      {
        EnsureAvailable();
        return new FileInfo(_contentPath).Length + new FileInfo(_associationPath).Length;
      }
    }
  }

  internal long GetContentSize(string collection, IEnumerable<byte[]> embeddingIds)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var metadataIds = new HashSet<int>();
      long size = 0;
      foreach (var id in embeddingIds)
      {
        var association = FindAssociation(collection, id);
        if (association is not null && metadataIds.Add(association.MetadataIndex)
          && _contentIndex.TryGet(association.MetadataIndex, out var current))
          size += _contents.GetItemLocation(checked((int)current)).Length;
      }
      return size;
    }
  }

  public long DeadBytes
  {
    get
    {
      lock (_gate)
      {
        EnsureAvailable();
        long total = 0;
        long live = 0;
        for (var index = 0; index < _contents.Count; index++)
        {
          total += _contents.GetItemLocation(index).Length;
          if (_contentIndex.TryGet(index, out var current))
            live += _contents.GetItemLocation(checked((int)current)).Length;
        }
        for (var index = 0; index < _associations.Count; index++)
        {
          var record = _associations[index];
          if (record.Kind == SharedMetadataMutationKind.MetadataRetired) continue;
          if (record.Kind != SharedMetadataMutationKind.Association
            || !_associationIndex.TryGet(EncodeKey(record.Collection, record.EmbeddingId), out var current)
            || current != index)
            total += _associations.GetItemLocation(index).Length;
        }
        return total - live;
      }
    }
  }

  public void Compact()
  {
    lock (_gate)
    {
      EnsureAvailable();
      var obsolete = GetFileNames().Where(path => path != Path.Combine(_directory, _manifestName + ".json"))
        .Select(path => Path.GetRelativePath(_directory, path).Replace(Path.DirectorySeparatorChar, '/')).ToArray();
      if (obsolete.Any(path => path.StartsWith("../", StringComparison.Ordinal)))
        throw new NotSupportedException("Compaction requires sources in the same owned directory.");
      _generations ??= new(_generationOptions);
      var generation = _generations.CreateGeneration();
      var contentPath = _generations.GetFilePath(generation, "contents");
      var associationPath = _generations.GetFilePath(generation, "associations");
      WithSources(contentPath, associationPath, (contents, associations) =>
      {
        for (var index = 0; index < _contents.Count; index++)
        {
          if (_contentIndex.TryGet(index, out var current))
            contents.Add(new(_contents[checked((int)current)].Content));
          else
          {
            contents.Add(new([]));
            associations.Add(new(string.Empty, [], index, SharedMetadataMutationKind.MetadataRetired));
          }
        }
        for (var index = 0; index < _associations.Count; index++)
        {
          var record = _associations[index];
          if (record.Kind == SharedMetadataMutationKind.Association
            && _associationIndex.TryGet(EncodeKey(record.Collection, record.EmbeddingId), out var current)
            && current == index)
            associations.Add(record);
        }
        contents.Flush();
        associations.Flush();
      });

      try
      {
        _generations.Activate(generation, ["contents", "contents.index", "associations", "associations.index"],
          obsolete, files => WithSources(files["contents"], files["associations"], (contents, associations) =>
          {
            if (contents.Count != _contents.Count) throw new InvalidDataException("Compacted metadata count differs.");
            for (var index = 0; index < contents.Count; index++)
            {
              var expected = _contentIndex.TryGet(index, out var current)
                ? _contents[checked((int)current)].Content : [];
              if (!contents[index].Content.AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("Compacted metadata content differs.");
            }
            var associationPosition = 0;
            for (var index = 0; index < _contents.Count; index++)
              if (!_contentIndex.TryGet(index, out _))
              {
                var retired = associations[associationPosition++];
                if (retired.Kind != SharedMetadataMutationKind.MetadataRetired || retired.MetadataIndex != index)
                  throw new InvalidDataException("Compacted retired metadata differs.");
              }
            for (var index = 0; index < _associations.Count; index++)
            {
              var record = _associations[index];
              if (record.Kind != SharedMetadataMutationKind.Association
                || !_associationIndex.TryGet(EncodeKey(record.Collection, record.EmbeddingId), out var current)
                || current != index) continue;
              var copy = associations[associationPosition++];
              if (copy.Kind != record.Kind || copy.Collection != record.Collection
                || copy.MetadataIndex != record.MetadataIndex || !copy.EmbeddingId.AsSpan().SequenceEqual(record.EmbeddingId))
                throw new InvalidDataException("Compacted association differs.");
            }
            if (associationPosition != associations.Count)
              throw new InvalidDataException("Compacted association count differs.");
            CloseSources();
          }));
        _contentPath = contentPath;
        _associationPath = associationPath;
        OpenSources();
      }
      catch { _failed = true; throw; }
    }
  }

  private void CloseSources()
  {
    try { _contents?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    finally
    {
      try { _associations?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
      finally
      {
        try { _associationIndex?.Dispose(); }
        finally { _contentIndex?.Dispose(); }
      }
    }
  }

  private static void WithSources(string contentPath, string associationPath,
    Action<StoredList<SharedMetadataContent, object>, StoredList<SharedMetadataAssociation, object>> action)
  {
    var contents = new StoredList<SharedMetadataContent, object>(new StoreListOptions
      { FilePath = contentPath, FlushInterval = null }, null);
    StoredList<SharedMetadataAssociation, object> associations = null;
    try
    {
      associations = new(new StoreListOptions { FilePath = associationPath, FlushInterval = null }, null);
      action(contents, associations);
    }
    finally
    {
      try { contents.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
      finally { associations?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
  }

  public int AddMetadata(byte[] content)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var index = _contents.Count;
      var record = new SharedMetadataContent(content.ToArray());
      try
      {
        _contents.Add(record);
        _contents.Flush();
        _contentIndex.Set(index, index);
      }
      catch { _failed = true; throw; }
      return index;
    }
  }

  internal int ContentRecordCount
  {
    get { lock (_gate) { EnsureAvailable(); return _contents.Count; } }
  }

  internal int SetEmbeddingContent(string collection, byte[] embeddingId, byte[] content)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var association = FindAssociation(collection, embeddingId);
      if (association is not null)
      {
        UpdateMetadata(association.MetadataIndex, content);
        return association.MetadataIndex;
      }
      var metadataIndex = AddMetadata(content);
      AssociateEmbedding(collection, embeddingId, metadataIndex);
      return metadataIndex;
    }
  }

  public void AssociateEmbedding(string collection, byte[] embeddingId, int metadataIndex)
  {
    lock (_gate)
    {
      EnsureAvailable();
      if (metadataIndex < 0 || !_contentIndex.TryGet(metadataIndex, out _))
        throw new ArgumentOutOfRangeException(nameof(metadataIndex));

      var key = EncodeKey(collection, embeddingId);
      var record = new SharedMetadataAssociation(collection, embeddingId.ToArray(), metadataIndex);
      try
      {
        _contents.Flush();
        var index = _associations.Count;
        _associations.Add(record);
        _associations.Flush();
        _associationIndex.Set(key, index);
      }
      catch { _failed = true; throw; }
    }
  }

  public bool UpdateMetadata(int metadataIndex, byte[] content)
  {
    lock (_gate)
    {
      EnsureAvailable();
      if (metadataIndex < 0 || !_contentIndex.TryGet(metadataIndex, out _)) return false;

      var contentIndex = _contents.Count;
      var record = new SharedMetadataContent(content.ToArray());
      try
      {
        _contents.Add(record);
        _contents.Flush();
        _associations.Add(new(string.Empty, [], metadataIndex,
          SharedMetadataMutationKind.MetadataUpdated, contentIndex));
        _associations.Flush();
        _contentIndex.Set(metadataIndex, contentIndex);
      }
      catch { _failed = true; throw; }
      return true;
    }
  }

  public bool DeleteEmbedding(string collection, byte[] embeddingId)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var association = FindAssociation(collection, embeddingId);
      if (association is null || association.Kind == SharedMetadataMutationKind.EmbeddingDeleted)
        return false;

      try
      {
        _contents.Flush();
        _associations.Add(new(collection, embeddingId.ToArray(), association.MetadataIndex,
          SharedMetadataMutationKind.EmbeddingDeleted));
        _associations.Flush();
        _associationIndex.Delete(EncodeKey(collection, embeddingId));
      }
      catch { _failed = true; throw; }
      return true;
    }
  }

  public byte[] GetContent(string collection, byte[] embeddingId)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var association = FindAssociation(collection, embeddingId);
      if (association is null || association.Kind == SharedMetadataMutationKind.EmbeddingDeleted)
        return null;

      if (!_contentIndex.TryGet(association.MetadataIndex, out var contentIndex))
        throw new InvalidDataException("Association references an unavailable metadata ID.");
      return _contents[checked((int)contentIndex)].Content.ToArray();
    }
  }

  public bool TryGetMetadataIndex(string collection, byte[] embeddingId, out int metadataIndex)
  {
    lock (_gate)
    {
      EnsureAvailable();
      var association = FindAssociation(collection, embeddingId);
      metadataIndex = association?.MetadataIndex ?? -1;
      return association is not null;
    }
  }

  private SharedMetadataAssociation FindAssociation(string collection, byte[] embeddingId)
  {
    return _associationIndex.TryGet(EncodeKey(collection, embeddingId), out var index)
      ? _associations[checked((int)index)] : null;
  }

  private static byte[] EncodeKey(string collection, byte[] embeddingId)
  {
    var collectionLength = KeyEncoding.GetByteCount(collection);
    var length = checked(8 + collectionLength + embeddingId.Length);
    if (length > 512)
      throw new ArgumentOutOfRangeException(nameof(embeddingId), "Composite key exceeds 512 bytes.");
    var key = new byte[length];
    BinaryPrimitives.WriteInt32LittleEndian(key, collectionLength);
    KeyEncoding.GetBytes(collection, key.AsSpan(4, collectionLength));
    BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(4 + collectionLength), embeddingId.Length);
    embeddingId.CopyTo(key, 8 + collectionLength);
    return key;
  }

  private void EnsureAvailable()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if (_failed) throw new InvalidOperationException("Reopen the metadata store after a failed commit.");
  }

  public bool TryGetContent(string collection, byte[] embeddingId, out byte[] content)
  {
    content = GetContent(collection, embeddingId);
    return content is not null;
  }

  public void Flush()
  {
    lock (_gate)
    {
      EnsureAvailable();
      try
      {
        _contents.Flush();
        _associations.Flush();
      }
      catch { _failed = true; throw; }
    }
  }

  public async ValueTask DisposeAsync()
  {
    lock (_gate)
    {
      if (_disposed) return;
      _disposed = true;
    }

    try
    {
      if (_contents is not null) await _contents.DisposeAsync();
    }
    finally
    {
      try
      {
        if (_associations is not null) await _associations.DisposeAsync();
      }
      finally
      {
        try { _associationIndex?.Dispose(); }
        finally
        {
          try { _contentIndex?.Dispose(); }
          finally { _generations?.Dispose(); }
        }
      }
    }
  }
}