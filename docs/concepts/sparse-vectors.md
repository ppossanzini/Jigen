# Sparse vectors and hybrid search

Jigen stores **two independent vectors** per entry: a dense embedding and an
optional **sparse lexical vector**. This page explains what the sparse slot is,
how it is searched, and how the two rankings are combined.

## What a sparse vector is

A sparse vector is a set of `(term, weight)` pairs. Everything not listed is
zero, so the vector is described by its non-zero entries only — the shape a
lexical representation naturally takes (one dimension per vocabulary term).

```
terms:   [12, 98, 450]
weights: [0.8, 1.2, 0.4]
```

Terms are ids and weights, and there are two ways to obtain them:

- **The caller supplies them.** Whatever produces the weights — BM25 on your side,
  SPLADE, a learned sparse encoder — runs in your process, and what arrives is
  already the vector.
- **The server generates them from a sentence.** The embedding module tokenizes the
  text and returns the terms it found: the term is the tokenizer's vocabulary id,
  the weight grows with the number of occurrences. No model output is involved, so
  it is deterministic and needs no extra artifact.

The second is what makes a write with only a sentence useful: both slots are filled
in one operation. It reads the tokenizer, so it behaves the same whatever model is
configured — a Hugging Face `tokenizer.json`, the SentencePiece sidecar used by
BGE-M3, and an ONNX tokenizer session (nomic's `tokenizer.onnx`) all produce terms
from the same rule, with the sequence markers excluded. The generated weights are **lexical, not learned** — a synonym
carries no weight, a common word weighs as much as a rare one, and a term matches
only when the same word appears on both sides. Corpus statistics (IDF) are
deliberately not part of it: they would turn the index into a full text engine with
a corpus state to maintain.

Either way the scoring rule is the same and trivial to state: the score is the
**dot product** over the terms a query shares with a document.

Rules the vector must satisfy:

| Rule | Why |
|---|---|
| Terms unique and strictly increasing | The representation is canonical: two vectors with the same pairs are identical in layout, not just in meaning |
| Terms non-negative | They are ids, not offsets |
| Weights finite and non-negative | A negative weight would let a term push a document down, which the scoring rule does not model |
| At least one weight above zero | An all-zero vector carries no signal; **omit** the vector instead |
| At most `SparseMaxTerms` terms (1024 by default) | Bounds the cost per entry; a longer vector is refused, not truncated |

An **absent** sparse vector and an **empty** one are different things: absent
means "this entry has no lexical representation", empty is rejected.

## The term index

Searching by sparse vector without an index would mean scoring every entry. The
**term index** maps each term to the entries carrying it (an inverted index), so a
query visits only the lists of its own terms.

- **In memory**, rebuilt from the data files at the first lexical search of a
  collection (K-38 requires rebuildability, not a separate persistence).
- **Exact**: the returned top-k is the true top-k of the dot product, not an
  approximation — there is no graph and no pruning.
- **Observable**: collection info reports distinct terms, postings, average list
  length, memory and retired terms.

Memory is measured, not assumed: at 100,000 entries with 6 terms each
(~50,000 distinct terms, 600,000 postings) the index occupies ~62 MB of managed
heap (624 bytes per entry), builds in under half a second, and an exact 6-term
search takes ~5 ms. See [server configuration](../server/configuration.md) for
the declared ceiling.

## Searching

### Lexical only

The entries sharing at least one term with the query, ranked by the sum of the
shared weight products. With a filter, every candidate is evaluated before the
cut, because scores alone cannot decide which documents the predicate will drop.

### Hybrid: fusion by position

A dense similarity and a lexical weight live on **different scales**: cosine
similarities cluster near 0.4–0.9 for a text model, while lexical weights can be
5 or 50. Summing them would let one modality bury the other.

Jigen therefore fuses the **rankings**, not the scores, with reciprocal rank
fusion: each hit contributes `weight / (60 + position)`, and the two
contributions are added. The constant 60 is the conventional value and it damps
the difference between neighbouring positions.

| Property | Consequence |
|---|---|
| Position-based | The two score scales are never compared |
| Weighted | A declared weight tunes each modality; zero removes it from the fusion |
| Deterministic | Equal fused scores break by ascending key, so the same input yields the same order |
| Traceable | Each hit reports which modality found it and its original score in each |

A hit found by **both** modalities outranks one found by a single modality at a
comparable position, which is the entire point of hybrid retrieval: dense search
finds paraphrases, lexical search finds exact terms, and their intersection is
usually the best answer.

Both weights must be declared when both vectors are supplied. A single modality
needs none.

## Generating from text

`IEmbeddingGenerator` exposes `GenerateSparseEmbedding(text)` next to the dense
methods, so one sentence yields both vectors. On the server this happens
automatically:

| What the write carries | What is stored |
| --- | --- |
| A sentence, no vectors | Dense and sparse, generated from it |
| A dense vector and a sentence | The caller's dense, the generated sparse |
| A sparse vector and a sentence | The generated dense, the caller's sparse |
| Both vectors | Exactly what the caller sent |
| No sentence | Nothing is generated: the entry keeps the vectors it was given |

A vector supplied by the caller always wins over a generated one, and the two
vectors of a sentence are computed in a single dispatch, so a hybrid query can
never observe a new dense vector next to an old lexical one.

```csharp
// Server side: one call, both slots.
await articles.AddAsync(42, new Article { Title = "Il parco è buio" },
    sentence: "il parco di via vittorio emanuele è buio");
```

The **search** side follows the same rule: a hybrid search accepts a sentence and
generates the two query vectors from it, so write and read can both start from
text.

```csharp
var hits = await articles.SearchHybridAsync("mancano lampioni nel parco", top: 10);
```

## Writing

The lexical slot is written **in the same operation** as the dense vector, through
the same write-ahead log record, CRC and fsync boundary: a hybrid query can never
observe an entry with the new dense vector and the old lexical one.

A write carrying terms **requires the write-ahead log**. Without it the slot could
not be recovered after a crash, so the write is refused with an explicit error
rather than silently made durable only at the next checkpoint. The log is enabled
by default in the server.

Delete, checkpoint and compaction treat the sparse files like the others: the
checkpoint fsyncs them before truncating the log, and compaction rewrites and
swaps them together with content, embeddings and the position index.

## See also

- [Store options](../in-process/store-options.md) — `Sparse`/`SparseIndex` and the sparse files on disk
- [Server configuration](../server/configuration.md) — `SparseEnabled`, `SparseMaxTerms`, the measured memory
- [REST API](../server/rest-api.md) — the sparse and hybrid search bodies
- [Client usage](../client/usage.md) — `SparseVector`, `SearchHybridAsync`
