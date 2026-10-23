using System.IO.MemoryMappedFiles;
using Jigen.DataStructures;

namespace Jigen;

public interface IStore
{
  MemoryMappedViewAccessor GetContentAccessor(long offset, long length);
  MemoryMappedViewAccessor GetEmbeddingAccessor(long offset, long length);

  bool GetCollectionIndexOf(string collection, out IReadOnlyDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, int vectorcount, long size)> index);

  bool TryGetContent(string collection, byte[] id, out byte[] content);
  byte[] GetContent(string collection, byte[] id);

  /// <summary>Every vector of the entry, or null when it has none.</summary>
  float[][] GetEmbeddings(string collection, byte[] id);

  long ContentSize { get; }

  Task Close();
  Task SaveChangesAsync(CancellationToken? cancellationToken);
  Task SaveIndexChanges();

  CollectionInfo GetCollectionInfo(string name);
  IEnumerable<string> GetCollections();
  IEnumerable<string> GetFileNames();
}