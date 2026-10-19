using System.Security.Cryptography;
using System.Text;
using Jigen.Extensions;
using Jigen.Persistance;

namespace Jigen;

public partial class Store
{
  private PersistentFileGeneration _migrationGeneration;
  private Guid? _activeStoreGeneration;
  private string MigrationManifestName => Options.DataBaseName + ".store-generation";

  private string ResolveDataFile(string name) => _activeStoreGeneration is { } generation
    ? Path.Combine(Options.DataBasePath, generation.ToString("N"), name)
    : Path.Combine(Options.DataBasePath, name);

  private void OpenOrMigrateGeneration()
  {
    var options = new FileGenerationOptions
    {
      DirectoryPath = Options.DataBasePath, ManifestName = MigrationManifestName
    };
    if (PersistentFileGeneration.HasActiveGeneration(options))
    {
      _migrationGeneration = new(options);
      _activeStoreGeneration = _migrationGeneration.ActiveGeneration;
      return;
    }

    var sharedPath = ResolveDataFile(Options.DataBaseName + ".shared.associations.jigen");
    var sharedExists = File.Exists(sharedPath) || PersistentFileGeneration.HasActiveGeneration(new FileGenerationOptions
    {
      DirectoryPath = Options.DataBasePath, ManifestName = Path.GetFileName(sharedPath) + ".generation"
    });
    var legacy = File.Exists(ContentFullFileName) && new FileInfo(ContentFullFileName).Length > 20;
    legacy |= File.Exists(IndexFullFileName) && new FileInfo(IndexFullFileName).Length > 0 && !sharedExists;
    if (!legacy) return;

    _migrationGeneration = new(options);
    var generation = _migrationGeneration.CreateGeneration();
    var entries = ReadMigrationIndex(IndexFullFileName);
    var contentName = Path.GetFileName(ContentFullFileName);
    var indexName = Path.GetFileName(IndexFullFileName);
    var embeddingName = Path.GetFileName(EmbeddingsFullFileName);
    var contentSource = ContentFullFileName;
    var indexSource = IndexFullFileName;
    var embeddingSource = EmbeddingsFullFileName;
    var sharedContentName = Options.DataBaseName + ".shared.contents.jigen";
    var sharedAssociationName = Options.DataBaseName + ".shared.associations.jigen";
    var destinationFiles = new List<string>
    {
      contentName, indexName, embeddingName, sharedContentName, sharedContentName + ".index",
      sharedAssociationName, sharedAssociationName + ".index"
    };
    var obsoleteFiles = new List<string> { contentName, indexName, embeddingName };
    _migrationGeneration.CopyFile(generation, embeddingName, embeddingSource);
    if (File.Exists(WalFullFileName))
    {
      var walName = Path.GetFileName(WalFullFileName);
      _migrationGeneration.CopyFile(generation, walName, WalFullFileName);
      destinationFiles.Add(walName);
      obsoleteFiles.Add(walName);
    }
    _migrationGeneration.PrepareFile(generation, contentName, file =>
    {
      using var writer = new BinaryWriter(file, Encoding.UTF8, leaveOpen: true);
      writer.Write((long)sizeof(long));
    });

    var references = new Dictionary<string, Dictionary<byte[], long>>();
    var candidate = new SharedMetadataStore(
      new StoreListOptions { FilePath = _migrationGeneration.GetFilePath(generation, sharedContentName), FlushInterval = null },
      new StoreListOptions { FilePath = _migrationGeneration.GetFilePath(generation, sharedAssociationName), FlushInterval = null });
    try
    {
      _migrationGeneration.PrepareFile(generation, indexName, file =>
      {
        foreach (var (collection, records) in entries)
        {
          references[collection] = new(ByteArrayEqualityComparer.Instance);
          foreach (var (id, record) in records)
          {
            VerifyLegacyEmbedding(embeddingSource, id, record.EmbeddingPosition, record.Dimensions);
            var content = ReadLegacyContent(contentSource, id, record.ContentPosition, record.Size);
            long reference = 0;
            if (content is not null)
            {
              var metadata = candidate.AddMetadata(content);
              candidate.AssociateEmbedding(collection, id, metadata);
              reference = metadata + 1L;
            }
            references[collection][id] = reference;
            StoreWritingExtensions.WriteIndexRecord(file, id, collection, reference,
              record.EmbeddingPosition, record.Dimensions, record.Size);
          }
        }
      });
      candidate.Flush();
    }
    finally { candidate.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

    _migrationGeneration.Activate(generation, destinationFiles, obsoleteFiles, files =>
    {
      var convertedIndex = ReadMigrationIndex(files[indexName]);
      if (convertedIndex.Count != entries.Count)
        throw new InvalidDataException("Converted collection count differs.");
      var converted = new SharedMetadataStore(
        new StoreListOptions { FilePath = files[sharedContentName], FlushInterval = null },
        new StoreListOptions { FilePath = files[sharedAssociationName], FlushInterval = null });
      try
      {
        foreach (var (collection, records) in entries)
        {
          if (!convertedIndex.TryGetValue(collection, out var copies) || copies.Count != records.Count)
            throw new InvalidDataException("Converted entry count differs.");
          foreach (var (id, record) in records)
          {
            if (!copies.TryGetValue(id, out var copy) || copy.ContentPosition != references[collection][id]
              || copy.EmbeddingPosition != record.EmbeddingPosition || copy.Dimensions != record.Dimensions || copy.Size != record.Size)
              throw new InvalidDataException("Converted index differs.");
            var expected = ReadLegacyContent(contentSource, id, record.ContentPosition, record.Size);
            var actual = converted.GetContent(collection, id);
            if ((expected is null) != (actual is null) || (expected is not null && !expected.AsSpan().SequenceEqual(actual)))
              throw new InvalidDataException("Converted content differs.");
          }
        }
      }
      finally { converted.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
      if (!FileChecksum(embeddingSource).AsSpan().SequenceEqual(FileChecksum(files[embeddingName])))
        throw new InvalidDataException("Converted vectors differ.");
      if (File.Exists(WalFullFileName) && !FileChecksum(WalFullFileName).AsSpan().SequenceEqual(FileChecksum(files[Path.GetFileName(WalFullFileName)])))
        throw new InvalidDataException("Converted WAL differs.");
    });
    _activeStoreGeneration = generation;
  }

  private static byte[] FileChecksum(string path)
  {
    byte[] checksum = null;
    PersistentFileGeneration.ReadFile(path, file => checksum = SHA256.HashData(file));
    return checksum;
  }

  private static Dictionary<string, Dictionary<byte[], (long ContentPosition, long EmbeddingPosition, int Dimensions, long Size)>>
    ReadMigrationIndex(string path)
  {
    var collections = new Dictionary<string, Dictionary<byte[], (long, long, int, long)>>();
    PersistentFileGeneration.ReadFile(path, file =>
    {
      using var reader = new BinaryReader(file, new UTF8Encoding(false, true), leaveOpen: true);
      while (file.Position < file.Length)
      {
        var id = ReadMigrationField(reader);
        var collection = new UTF8Encoding(false, true).GetString(ReadMigrationField(reader));
        var content = reader.ReadInt64();
        var embedding = reader.ReadInt64();
        var dimensions = reader.ReadInt32();
        var size = reader.ReadInt64();
        if (!collections.TryGetValue(collection, out var records))
          collections[collection] = records = new(ByteArrayEqualityComparer.Instance);
        if (content == IndexTombstone && embedding == IndexTombstone)
        {
          records.Remove(id);
          continue;
        }
        if (content < 0 || embedding < 0 || dimensions < 0 || size < 0 || size > int.MaxValue
          || (content == 0 && size != 0) || (embedding == 0 && dimensions != 0))
          throw new InvalidDataException("Legacy index record is invalid.");
        records[id] = (content, embedding, dimensions, size);
      }
    });
    foreach (var collection in collections.Where(item => item.Value.Count == 0).Select(item => item.Key).ToArray())
      collections.Remove(collection);
    return collections;
  }

  private static byte[] ReadMigrationField(BinaryReader reader)
  {
    var length = reader.ReadInt32();
    if (length <= 0 || length > 65536 || length > reader.BaseStream.Length - reader.BaseStream.Position)
      throw new InvalidDataException("Legacy key field is invalid.");
    return reader.ReadBytes(length);
  }

  private static byte[] ReadLegacyContent(string path, byte[] id, long position, long size)
  {
    if (position == 0) return null;
    byte[] content = null;
    PersistentFileGeneration.ReadFile(path, file =>
    {
      if (position < sizeof(long) || position > file.Length - ContentRecordSize(id.Length, size))
        throw new InvalidDataException("Legacy content extent is invalid.");
      file.Position = position;
      using var reader = new BinaryReader(file, Encoding.UTF8, leaveOpen: true);
      if (!ReadMigrationField(reader).AsSpan().SequenceEqual(id) || reader.ReadInt32() != size)
        throw new InvalidDataException("Legacy content identity or length differs.");
      content = new byte[checked((int)size)];
      file.ReadExactly(content);
    });
    return content;
  }

  private static void VerifyLegacyEmbedding(string path, byte[] id, long position, int dimensions)
  {
    if (position == 0) return;
    PersistentFileGeneration.ReadFile(path, file =>
    {
      if (position < sizeof(long) || position > file.Length - EmbeddingRecordSize(id.Length, dimensions))
        throw new InvalidDataException("Legacy vector extent is invalid.");
      file.Position = position;
      using var reader = new BinaryReader(file, Encoding.UTF8, leaveOpen: true);
      if (!ReadMigrationField(reader).AsSpan().SequenceEqual(id))
        throw new InvalidDataException("Legacy vector identity differs.");
    });
  }
}