# Sparse index (lexical and learned sparse)

Alongside the dense HNSW graph, every collection can carry a **sparse** index: a document is described by `(term id, weight)` pairs, and a query scores documents by the weights of the terms they share with it. This is the classic inverted-index model, and it accepts any sparse representation you can produce — BM25-style term weights, SPLADE expansions, learned sparse vectors.

The index itself does **not** tokenize text or invent term ids: the writer decides the vocabulary, and the query must use the same one. Term `1204` in a document and term `1204` in a query mean the same thing because your indexer said so, which keeps the engine independent of any language, analyzer or model.

The embedding module can produce those pairs for you: `IEmbeddingGenerator.GenerateSparseEmbedding` returns the tokenizer terms of a text and their weights (TF with a log damping), for every model profile, so the same text that yields the dense embedding yields the sparse one. See [Producing the pairs](#producing-the-pairs).

## Where the pairs live

The pairs are part of the document record, next to its dense vectors, one sparse vector per dense vector (an entry with three passage vectors can carry three sparse vectors):

```csharp
await store.AppendContent(new VectorEntry
{
  Id = key,
  CollectionName = "articles",
  Content = content,
  Embeddings = [passage1, passage2],
  SparseVectors =
  [
    [new SparseTerm(1204, 1.0f), new SparseTerm(77, 0.4f)],   // terms of passage 1
    [new SparseTerm(1204, 0.8f)]                              // terms of passage 2
  ]
});
```

The stored pairs are the source of truth: the inverted index is derived from them and can be rebuilt. Term ids must be non-negative, weights finite, and a term cannot repeat inside one sparse vector.

## Search

Three entry points on `VectorCollection<T>` (and on the client):

| Method | What it searches |
|---|---|
| `SearchDense(vector or sentence, ...)` | dense half only — the HNSW graph |
| `SearchSparse(query, top, predicate, minimumScore)` | sparse half only — the inverted index |
| `Search(embeddings, sparseQuery, top, ...)` | both halves, fused (hybrid) |

```csharp
// Sparse only: term id -> query weight.
var sparse = articles.SearchSparse(new Dictionary<int, float> { [1204] = 1.0f, [77] = 0.4f }, top: 10);

// Hybrid: reciprocal-rank fusion of the two halves.
var hybrid = articles.Search(embeddings: queryVector, sparseQuery: sparse, top: 10);

// Weak hits dropped before the fusion, so a strong half cannot lift a weak document.
var tuned = articles.Search(queryVector, sparse, top: 10,
  minDenseScore: 0.30, minSparseScore: 0.05);
```

A document scores the **maximum inner product over its vectors** against the query weights, and it occupies one result slot no matter how many of its vectors matched. `Score` on a hybrid result is the fused value; `DenseScore` and `SparseScore` keep the partial scores that produced it.

**Fusion.** Dense similarities and sparse weights live on different scales, so the hybrid search fuses them by rank: a document scores `1 / (60 + position)` in each list where it appears, and the contributions are summed. Ranks are recomputed after the per-half cuts, so dropping weak hits leaves no gaps.

**Filtering.** The predicate is translated to the same serialized filter the dense search uses. A dense candidate is filtered without reading its content; a sparse candidate needs its content read first, so the predicate is only evaluated for documents that can still enter the top-k.

**`minimumScore`.** It drops documents below the score and *also* raises the pruning threshold before the top-k is full, so it skips whole blocks instead of scoring them.

## Producing the pairs

The embedding module turns text into `(term id, weight)` pairs with the tokenizer
of the configured model — no separate sparse model is involved, so the ids are
the model's vocabulary ids and documents and queries must be produced by the same
model:

```csharp
SparseEmbedding sparse = generator.GenerateSparseEmbedding("a lexical query");
// terms: vocabulary ids, strictly increasing; weights: lexical (TF with log damping)
// or, for BgeM3 with Sparse.HeadPath, BGE-M3's learned weights
```

For `BgeM3` the ids are the model's XLM-R vocabulary ids (the profile applies the
SentencePiece → XLM-R mapping) and the sequence markers are excluded. The ONNX
export carries no sparse head, so by default the weights are lexical (TF). Point
`SparseEmbeddingOptions.HeadPath` at BGE-M3's `sparse_linear.pt` to get the
model's learned weights instead: a distinctive word then weighs more than a
common one. See [BGE-M3 configuration](../embeddings/configuration.md#example-bge-m3).
Switching between the two changes every weight, so re-index the collection and
re-tune `minSparseScore`.

`SparseEmbeddingOptions` (`EmbeddingGeneratorOptions.Sparse`) controls it:
`MaxTerms` (default 1024) caps the vector; for the lexical weights `Offset` is the
weight of a single occurrence and `LogBase` damps repetitions, so a word used
twice weighs more than once without dominating the dot product. Words the
tokenizer splits into subword pieces contribute one term per piece, which is
what makes the sparse side work without an analyzer.

Over the network the same values are available from two places:

- the `CalculateSparseEmbedding` / `CalculateSparseEmbeddingBatch` RPCs (and the
  REST embedding endpoints), for callers that want the pairs in hand;
- the `SparseFromSentence` flag on `SearchDocument` and on document writes: the
  server derives the sparse side from the sentence it is already embedding, so a
  write fills both slots and a hybrid search starts from a sentence alone. The
  flag defaults to on when the transport does not send it; send `false` to keep
  the request dense-only.

An explicit `Sparse`/`SparseQuery` always wins over the derived one.

## Structure on disk

```
{db}.sparse/<collection>/
  manifest                  # the live segment generations, replaced atomically
  tombstones (+ .index)     # retired document copies
  segments/g<generation>/
    terms, terms.index      # term directory: term id -> posting ranges
    blocks, blocks.index    # posting blocks, at most 64 postings each
```

- **Segments are immutable and one per commit.** Writes never edit an existing segment, so they may arrive in any order and a document may be updated afterwards. A search asks every live segment and merges the results; because a document lives in exactly one live segment, the best k of the collection are among the best k of each segment.
- **A delete or an update writes a tombstone** `(documentId, upToGeneration)` that hides the copies in segments at or below that generation. The new version, written in a later segment, stays visible. A copy still in the commit buffer is simply dropped from it.
- **Compaction** merges every live segment into one, drops the retired copies and clears the tombstones. It runs as part of `Store.ShrinkAsync` and in the background worker described below.

## Lifecycle in the store

Writes are indexed by the store's writer thread after they are persisted, so the index follows the same single-writer rule as the data files. Recovery replays the write-ahead log: an insert re-indexes the document, and a key whose last record is a delete is not indexed again. Deleting a document, clearing a collection and shrinking all keep the sparse side in step with the dense one.

Batch facts worth knowing:

- **Buffered commits.** Indexed documents are buffered per collection and written as a segment only when the buffer holds at least `SparseOptions.CommitMinTerms` distinct terms (default 1000) or `SparseOptions.CommitMaxDelay` has passed since the collection's last commit (default 60 s). Until then they are not searchable. `Store.FlushSparseIndexes()`, a WAL checkpoint, a shrink and `Close` commit every buffer. Without the WAL, a crash loses the buffered documents from the sparse index (the dense side is not affected).
- **Background merges by size.** A worker runs every `SparseOptions.MaintenanceInterval` (default 5 s). It commits the buffers past the maximum delay, then merges segments by size, not by count. A segment that reached `MaxSegmentBytes` (default 256 MB) is never merged again. The smaller ones merge with newer segments of similar size, so each byte is rewritten only a few times. Segments under `MinSegmentBytes` (default 1 MB) count as that size, so tiny ones merge as soon as a second one appears. A partial merge keeps the tombstones, so retired copies in large segments leave the disk only at `ShrinkAsync`. Ingestion into the sparse index waits while a collection is merged. A zero interval disables the worker; `MaxSegmentBytes` of zero disables only the merges.
- **Deletes cost until compaction.** A `Clear` or a delete-heavy workload leaves segments behind with tombstones; search keeps reading them until a shrink compacts the collection. Measured on a 10.000-document collection, the same query took 6,5 ms with two segments and 3,7 ms after the shrink.

On the server the same options live under `JigenServer:Sparse` (`CommitMinTerms`, `CommitMaxDelay`, `MaxSegmentBytes`, `MinSegmentBytes`, `MaintenanceInterval`; durations as `hh:mm:ss`). A full shrink can also be started from the Databases page of Jigen Insight or with `POST /api/database/{name}/shrink`.

## Monitoring it

`GetCollectionInfo` (gRPC and REST) carries a `Sparse` block next to the dense
index metrics: segments, distinct terms, posting blocks, block size, tombstones
and the on-disk size of the collection's sparse directory. `DatabaseDetails`
adds `SparseSize` to the database totals, so the sparse footprint is not missing
from a disk-usage view.

Two numbers are worth watching: `Segments` and `Tombstones`. Searches read every
segment and merge, so a collection that writes often and never shrinks pays for
it in latency; `ShrinkAsync` compacts the segments to one and clears the
tombstones, dropping the retired copies from disk too.

## Cost model

Sparse search cost is dominated by **query terms × posting-list length**, not by corpus size. Measured on 30.000 documents carrying 3,6 M postings:

| Vocabulary | Query terms | Sparse search, k=10 |
|---|---|---|
| 200 terms (every term in ~60% of the documents) | 60 | 336 ms |
| 200 terms | 20 | 82 ms |
| 200 terms | 5 | 9,9 ms |
| 50.000 terms (every term in ~70 documents) | 20 | **0,89 ms** |

The index costs about 18,5 B per posting on disk. Full figures, including hybrid and transport costs, are in [Benchmarks](../benchmarks.md#sparse-and-hybrid-search).

## Limits

- The index has no analyzer of its own: pairs come from the writer, either computed by the embedding module (`GenerateSparseEmbedding`, `CalculateSparseEmbedding`, `SparseFromSentence`) or from your own indexer. Documents and queries must share the same vocabulary — and therefore the same model.
- A query only matches documents indexed with the same vocabulary; mixing vocabularies inside one collection returns nothing for the terms that do not line up.
- Legacy archives written before sparse vectors existed open dense-only and reject sparse writes with `NotSupportedException`.
- Sparse pairs can be written over gRPC and REST (`sparse` on `SetVector`, `SetVectors`, `SetRawVector`, `SetDocument`, `SetDocuments` and the append variants) — see the [gRPC API](../server/grpc-api.md) and [REST API](../server/rest-api.md).
