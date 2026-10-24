using System.Globalization;
using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Writes a collection as a sequence of immutable segments. Documents are
/// buffered and become searchable when <see cref="Commit"/> writes them; keys may
/// arrive in any order, and a document added after a commit lands in a later
/// segment.
/// <para>
/// An update or a deletion records a tombstone covering every segment written so
/// far. A new version is then written to the next segment, so it stays visible.
/// </para>
/// </summary>
public sealed class SparseCollectionWriter : IDisposable
{
    private readonly string _directoryPath;
    private readonly StoredList<SparseTombstone, ValueTuple> _tombstones;
    private readonly List<(byte[] DocumentId, IReadOnlyList<SparseTerm[]> Vectors)> _buffered = [];
    private readonly List<SparseTombstone> _retired = [];
    private readonly SparseTombstone[] _retiredLoaded;
    private long _lastGeneration;
    private bool _disposed;

    /// <summary>Postings per block.</summary>
    public int BlockSize { get; init; } = 64;

    public SparseCollectionWriter(string collectionDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionDirectoryPath);
        _directoryPath = collectionDirectoryPath;
        Directory.CreateDirectory(_directoryPath);
        Directory.CreateDirectory(SegmentsPath);

        var generations = SparseCollectionManifest.Read(_directoryPath);
        _lastGeneration = generations.Count == 0 ? 0 : generations[^1];
        _tombstones = new StoredList<SparseTombstone, ValueTuple>(
            new StoreListOptions { FilePath = Path.Combine(_directoryPath, "tombstones") }, default);
        _retiredLoaded = new SparseTombstone[_tombstones.Count];
        for (var position = 0; position < _retiredLoaded.Length; position++)
            _retiredLoaded[position] = _tombstones[position];
        _retired.AddRange(_retiredLoaded);
    }

    /// <summary>
    /// Every tombstone of the collection, including the ones written by this
    /// writer and not yet visible to another handle on the same file.
    /// </summary>
    public IReadOnlyList<SparseTombstone> Tombstones => _retired;

    /// <summary>Generation of the most recently committed segment.</summary>
    public long LastGeneration => _lastGeneration;

    /// <summary>Documents waiting for the next commit.</summary>
    public int BufferedDocuments => _buffered.Count;

    internal string SegmentsPath => Path.Combine(_directoryPath, "segments");

    internal static string SegmentPath(string segmentsPath, long generation) =>
        Path.Combine(segmentsPath, "g" + generation.ToString(CultureInfo.InvariantCulture));

    /// <summary>Buffers a document; it becomes searchable at the next commit.</summary>
    public void Add(byte[] documentId, IReadOnlyList<SparseTerm[]> vectors)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(vectors);
        if (documentId.Length == 0)
            throw new ArgumentException("A document requires a key.", nameof(documentId));

        _buffered.Add((documentId, vectors));
    }

    /// <summary>
    /// Retires every copy of the document written so far. Buffered documents are
    /// committed first, so the tombstone never hides a document queued in this
    /// batch; a version added afterwards stays visible.
    /// </summary>
    public void Delete(byte[] documentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(documentId);
        if (documentId.Length == 0)
            throw new ArgumentException("A document requires a key.", nameof(documentId));

        if (_buffered.Count > 0)
            Commit();

        if (_lastGeneration == 0)
            return;

        var tombstone = new SparseTombstone(documentId, _lastGeneration);
        _tombstones.Add(tombstone);
        _tombstones.Flush();
        _retired.Add(tombstone);
    }

    /// <summary>
    /// Writes the buffered documents as a new segment and publishes it in the
    /// manifest. Returns the generation written, or 0 when nothing was buffered.
    /// </summary>
    public long Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_buffered.Count == 0)
            return 0;

        var generation = _lastGeneration + 1;
        var builder = new SparseIndexBuilder { BlockSize = BlockSize };
        foreach (var document in _buffered)
            builder.Add(document.DocumentId, document.Vectors);
        builder.Write(SegmentPath(SegmentsPath, generation));

        _lastGeneration = generation;
        SparseCollectionManifest.Write(_directoryPath, SparseCollectionManifest.Read(_directoryPath).Append(generation));
        _buffered.Clear();
        return generation;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tombstones.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
