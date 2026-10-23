# Development Dossier

## DEV-GRANITE-R2-CONFIGURATION

Status: Completed.

- Added standalone CPU configurations for Granite Embedding 311M and 97M Multilingual R2.
- Added a dedicated `Granite` processing profile with right padding, CLS pooling and L2 normalization, without the BGE-M3-specific token-id mapping.
- Set passage defaults to 512 tokens with 64-token overlap for the accuracy-oriented 311M model and 384 tokens with 64-token overlap for the latency-oriented 97M model.
- Added direct Hugging Face `tokenizer.json` loading through the official Rust tokenizer bindings for .NET. Both configurations now use the tokenizer asset published by IBM.
- Verification: both files parsed as JSON and satisfied all `PassageSplittingOptions` startup constraints. `Jigen.SemanticTools` and `Jigen.Embedding.Handlers` build successfully.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-GRANITE-R2-MODEL-VALIDATION

Status: Completed.

- Deployed the official 97M and 311M tokenizer JSON and quantized AVX2 ONNX assets under `/data/onnx`.
- Verified all four assets against the published Hugging Face LFS SHA-256 hashes.
- Added real-model tests for tokenization, output dimensions, finite values, L2 normalization, Italian semantic ranking, and batch generation.
- Verification: 8 Granite tests passed. The complete `Jigen.SemanticTools.Tests` suite passed 32 tests.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`, and `run-tests`.

## DEV-GRANITE-97M-INDEX-BENCHMARK

Status: Completed.

- Indexed 1,000 texts with Granite 97M and Jigen HNSW.
- Executed 2,000 generated searches against the persisted index.
- Expected-document top-1 accuracy was 57.40%. First-query accuracy was 50.50%. Second-query accuracy was 66.00%.
- Cosine score minimum, average, and maximum were 0.757785, 0.865203, and 0.965269.
- Generated `/data/score.txt` and persisted the index under `/data/granite-97m-test-index`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-GRANITE-PARAMETER-EVALUATION

Status: Completed.

- Compared Granite 97M at 384 dimensions with Granite 311M at 128, 256, 384, 512, and 768 dimensions over 2,000 searches.
- Granite 311M at 768 dimensions reached 69.30% exact top-1. Granite 97M reached 58.70%.
- HNSW with `M=32`, `EfConstruction=400`, and `EfSearch=128` reached 68.95% top-1 and 98.75% exact-result agreement.
- Granite 311M at 512 dimensions reached 69.05% exact top-1.
- A 512-token passage size splits 9 of 1,000 documents. No document exceeds 1,024 tokens.
- Generated `/data/granite-parameter-results.txt`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-EMBEDDING-MODEL-COMPARISON-FIRST100

Status: Completed.

- Compared exact retrieval over the first 100 documents and 200 generated queries.
- BGE-M3 reached 96.50% top-1. Nomic v1.5 INT8 reached 96.00%.
- Granite 311M reached 85.00% at both 512 and 768 dimensions. Granite 97M reached 81.00%.
- Nomic encoded the sample in 12.170 seconds. BGE-M3 required 50.526 seconds.
- Generated `/data/model-comparison-first100.txt`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-HNSW-BALANCED-PRODUCTION-PROFILE

Status: Completed.

- Configured `M=32`, `EfConstruction=400`, and `EfSearch=128` in the server appsettings.
- Kept graph quantization disabled and exact reranking enabled.
- The measured profile reached 98.75% exact-result agreement on 2,000 searches.
- Existing HNSW graphs require rebuilding before `M` and `EfConstruction` take full effect.
- Verification: server configuration validation and server build succeeded.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-NOMIC-OPTIMIZED-CONFIGURATION

Status: Completed.

- Added a standalone Nomic INT8 CPU configuration with explicit `Nomic` profile and 768-dimensional output.
- Allocated two embedding workers with 10 ONNX intra-op threads each on the 22-logical-CPU host.
- Set 512-token passages with 64-token overlap for the short-report corpus.
- Set `search_document` as the default task and exposed the four Nomic task prefixes.
- Verification: JSON constraints, embedding worker build, and the real-model Nomic HNSW score test succeeded.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, `capabilities-dev-problem-solving-methodology`, and `run-tests`.

## DEV-NOMIC-SERVER-CONFIGURATION

Status: Completed.

- Added a standalone all-in-one server profile for the Nomic INT8 text model.
- Combined the optimized Nomic worker settings with the balanced HNSW `32/400/128` profile.
- Preserved server identity, storage, and RabbitMQ settings. Image embeddings remain disabled.
- Verification: JSON constraints and server build succeeded. The environment file was copied to the runtime output.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## DEV-NOMIC-TASK-ROUTING

Status: Completed.

- Confirmed that document ingestion and sentence search previously omitted the embedding task and both inherited `search_document` from `DefaultTask`.
- Document ingestion now requests `search_document`. REST and gRPC sentence searches request `search_query`.
- Server build succeeded. The focused API test was added but execution is blocked by a pre-existing constructor error in `EmbeddingControllerTests`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, `base-dossier-writing`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`.

## Technology resolution

| Slot | Technology | Implementation skill |
| --- | --- | --- |
| Backend platform | .NET 10 / C# | `implementation-be-dotnet-dev` |
| Embedding runtime | ONNX Runtime 1.27 | No dedicated implementation skill; existing project implementation applies |

Task `DEV-BGE-M3-PROFILE` changes the local ONNX embedding profile contract and documentation. Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `base-be-base-rules`, `implementation-be-dotnet-dev`, and `capabilities-dev-problem-solving-methodology`. No capability skill beyond problem-solving is required.

## DEV-BGE-M3-PROFILE

Status: Completed.

- Added the `BgeM3` processing profile with right padding, CLS pooling, and L2 normalization.
- Documented the official Hugging Face ONNX file layout and configuration.
- Scope is dense embeddings only; sparse and ColBERT outputs remain excluded.
- Verification: `dotnet build src/Jigen/Jigen.SemanticTools/Jigen.SemanticTools.csproj --no-restore` succeeded with no errors. The existing `NETSDK1206` warning concerns macOS runtime identifiers in `Microsoft.ML.OnnxRuntime.Extensions`.
- Editor diagnostics reported no errors in the modified C# files.
- Tests were not modified or added because production code and tests are separate tasks under the backend baseline.

## DEV-BGE-M3-HNSW-SCORE

Status: Failed as expected; the regression is reproduced.

- Added a focused integration test for BGE-M3 embeddings stored and searched through `Jigen.Store` with `SmallWorldIndexer`.
- The test proves that HNSW returns the direct cosine unchanged: both values are `0.6735` for ticket `00020294` and query `prostituzuione`.
- The expected reference similarity is about `0.3355`, so the inflation occurs during embedding generation, before HNSW search.
- A focused tokenizer test confirms the cause: for `prostituzuione`, the official tokenizer emits `[0, 44794, 1667, 20493, 2]`, while the current Microsoft SentencePiece construction emits `[1, 44793, 1666, 20492, 2]`. The BOS id is wrong and every lexical token id is shifted by `-1`.
- Verification: the focused test built successfully and failed on the reference-score assertion with direct cosine `0.6735` and HNSW score `0.6735`.
- Verification: the tokenizer test built successfully and failed with the exact divergent token sequences above.
- Existing analyzer warnings in `VectorCollectionTests.cs` and the existing ONNX Runtime RID warning remain outside this task.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-BGE-M3-TOKENIZER-FIX

Status: Completed.

- Corrected the BGE-M3 JSON-tokenizer path by mapping raw SentencePiece ids to the XLM-RoBERTa vocabulary expected by the ONNX model.
- The mapping is restricted to the `BgeM3` profile; other profiles and ONNX tokenizers are unchanged.
- Verification: `Jigen.SemanticTools` built with 0 warnings and 0 errors.
- Verification: the focused BGE-M3 Store/HNSW regression test passed, proving the corrected end-to-end similarity.
- Verification: the focused BGE-M3 public-generator test passed with similarity in the expected `0.33`–`0.34` interval.
- Verification: the Nomic Store/HNSW test passed. The unrelated query scored `0.530176`, the relevant query scored `0.634335`, and both HNSW scores matched their direct cosine values.
- Existing xUnit analyzer warnings in `VectorCollectionTests.cs` remain outside this task.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-BGE-M3-IGNORE-TASK

Status: Production code verified; awaiting code-quality approval before the separate test update.

- BGE-M3 now ignores task text during input preparation, including explicit client values and stale non-empty defaults.
- Nomic and Qwen task formatting remains unchanged; SigLIP2 continues to ignore tasks.
- Verification: `Jigen.Embedding.Handlers` built with 0 errors. Two existing ONNX Runtime RID warnings remain.
- Verification: the BGE-M3 Store/HNSW regression test passed.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-VECTOR-FILE

Status: Completed.

- The embeddings file record is now `[id length][id][vector count][vector count × dimensions × float]`, so one entry can store several vectors.
- `VectorEntry.Embeddings` carries the vectors; `Embedding` remains the single-vector shorthand, and `Vectors` resolves whichever is set.
- The position index carries the vector count (`{name}.index.jigen` record and in-memory tuple), so delete accounting, shrink, collection metrics and the embeddings record size stay exact.
- The WAL insert record carries `[vector count][dimensions][vectors]`; replay restores every vector.
- Exact (brute-force) scoring ranks a multi-vector entry by its closest vector. The HNSW index keeps one node per document and indexes the first vector.
- `GetEmbeddings` reads all vectors at store, gRPC (`Vector.Vectors`, `EmbeddingResponse.Vectors`), REST (`VectorPayload.Vectors`, `BulkVectorItem.Vectors`, transaction payloads) and client level; `GetEmbedding` keeps returning the first vector.
- Existing databases written with the previous layout are not readable by this version.
- Verification: `Jigen.Primitives`, `Jigen.Store`, `Jigen.Indexer.HNSW`, `Jigen.API`, `Jigen.Grpc`-hosted server, `Jigen-AllInOne` and `Jigen.Client` built with 0 errors.
- Verification: `MultiVectorTests` (5 tests) passed: round trip, reopen, WAL replay, max-similarity scoring, mixed-vector rejection.
- Verification: `JigenStoreTests` passed 70 of 78; the 8 failures are the pre-existing database-lock collisions of `IngestionTests` and `SeedDemoDatabaseTest`, which share `/data/jigendb`.
- Verification: `PrimitiveTests` (14) and the HNSW split-storage tests (4) passed. `Jigen.API.Tests` still fails to build on the pre-existing `EmbeddingControllerTests` constructor error; `Jigen.Grpc.Tests` fails on the pre-existing `Server.Server` constructor signature.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-BENCHMARK

Status: Completed.

- Extended the macro-benchmark with `mv:K` (K vectors per document) and with the vectors/index file sizes, plus a tail-recall metric measured on a non-first vector.
- Single-vector path shows no regression against the pre-change code (HEAD in a temporary worktree): ingest 1683–2091 vs 1843–2091 docs/s, HNSW search 595–671 vs 575–630 µs, recall 0.986–0.995 vs 0.992–0.996 — all within run-to-run noise.
- Multi-vector storage is exact at every K: the vectors file equals documents × K × dimensions × 4 B plus 24 B of header per document.
- Ingest cost per document is flat (~1.5–1.7 k docs/s at 10k) while stored vectors scale linearly.
- HNSW keeps one node per document, so recall against exact max-similarity search falls to chance as K grows; a query on a non-first vector is not reachable through the graph (0.18 at K=4, 0.03 at K=32).
- Exact search cost grows linearly with K (1.8 ms at K=1 to 6.6 ms at K=32 on 10k documents).
- Verification: results recorded in `docs/benchmarks.md` under "Multi-vector entries".
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-HNSW-NODES

Status: Completed.

- Corrected the previous behavior: the graph never required one node per document. It accepts several nodes sharing a key (the delete lookup already kept a position list per key, and search already merged same-key nodes by best score), so indexing every vector needed no graph-format change at all.
- `WriterThread` now enqueues one index entry per stored vector instead of only the first.
- `ReconcileAsync` re-indexes every vector of a missing key, so a partial graph is rebuilt with the same shape as a normal insert.
- SQ8 exact reranking now rescores a candidate against the document's best stored vector, matching the exact index rule; it previously read only the first vector.
- Added `HnswCollection_FindsDocumentThroughAnyOfItsVectors` (round trip through three vectors of one document).
- Measured (10k docs, 128 dim): recall@10 0.902 and 0.935 on a non-first vector at K=4, versus 0.331 and 0.181 with first-vector-only indexing; graph grows to 28.35 MB (from 7.09 MB) and ingest drops to 372 docs/s, as expected from K× nodes. `mv:1` is unchanged.
- Verification: `Jigen.Store` and `Jigen.Indexer.HNSW` built with 0 errors.
- Verification: `JigenStoreTests` 64 passed and `HnswTest` 20 passed, excluding the classes that collide on `/data/jigendb`.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-QDRANT-BENCHMARK

Status: Completed.

- Ran the comparative macro-benchmark (`jigendb-hnsw-w8` vs `qdrant`, `random-10k`) against a local Qdrant 1.19.2 container: Jigen post-index P50 0.22 ms vs 3.12 ms, both recall 1.000, ingest 4,066 vs 33,425 vec/s.
- Verified Qdrant's multi-vector statistics empirically: 40,000 stored vectors over 10,000 points report `points_count: 10000` and `indexed_vectors_count: 10000`. Both counters are per point; the API exposes no vector count.
- Verified Qdrant de-duplicates multi-vector hits per point (10 results, 10 unique ids), the same pooling Jigen applies when several graph nodes share a document key.
- Measured the like-for-like multi-vector workload (10k documents × 4 vectors × 128 dimensions): Qdrant ingest 1,989 docs/s plus 3.0 s indexing, search P50 4.94 ms over REST; Jigen `mv:4` 372 docs/s including graph build, search 910 µs in-process.
- Kept the probe reproducible in `.ai-generated-operations/qdrant-multivector-probe.py`.
- Verification: `MultiVectorTests` (6 tests, including collection statistics) passed.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-LAZY-INDEXING-VERIFICATION

Status: Completed.

- Confirmed the lazy indexing parameters: `LazyIndexer(Func<IIndexer>, int threshold = 100001)` in the store layer, `jigendb-lazy-wN` (threshold 9,999, or `-t50000`) in the comparative suite, and a new `lazy` / `lt:N` mode in the macro-benchmark.
- Measured lazy vs eager (10k docs, 128 dim): lazy ingest 77,560 docs/s at K=1 and 43,685 docs/s at K=4 (versus 1629 and 331), but the deferred build is one serial reconciliation pass — 19.64 s and 119.68 s — so time to a searchable graph is 3.2× and 4.0× worse than eager, which builds concurrently across the index workers.
- Recall is unchanged by lazy indexing, including through a non-first vector (0.932 at K=4), because reconciliation now re-indexes every stored vector.
- `LazyIndexer` compares the collection's position-index size, so the threshold counts documents, not vectors.
- Found a measurement artifact in the comparative suite: its pre-index phase issues 5 searches and the first one triggers the lazy switch, so the build cost is charged to the pre-index sample and the `Index (s)` column reports only a second no-op pass (0.02 s). Documented in `docs/benchmarks.md`; the suite itself is unchanged.
- Verification: macro-benchmark runs for eager/lazy at K=1 and K=4, and comparative macro for `jigendb-lazy-w8`, `jigendb-hnsw-w8` and `qdrant` on `random-10k`.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-MULTIVECTOR-CLIENT-CONTRACT

Status: Completed.

- Found the Python client bindings stale: `_proto/Jigen_pb2.py` was generated from the pre-change proto, so `EmbeddingVector`, `Vector.Vectors` and `EmbeddingResponse.Vectors` did not exist and the client could not send or read multi-vector entries.
- Regenerated the Python bindings from `Jigen.Client/Protos/Jigen.proto` with `grpc_tools.protoc` and restored the package-relative import in `Jigen_pb2_grpc.py`.
- Extended the Python client: `embedding=` now accepts one vector or a list of vectors (single goes to the compatible `Embeddings` field, several to `Vectors`), `upsert_many` accepts both shapes per entry, and `get_embeddings` returns every vector while `get_embedding` keeps returning the first.
- Documented the multi-vector usage in the Python README.
- Verified end to end against a live .NET gRPC server: multi-vector upsert, read back, search by each of the three vectors, single-vector fallback, bulk streaming path, and the async client.
- Recorded the reproduction in `.ai-generated-operations/check-python-multivector.py`.
- Note: the all-in-one host (`Jigen-AllInOne.csproj`) has the `Jigen.Grpc` reference commented out, so it serves no gRPC endpoints and every call falls through to the SPA fallback; the gRPC endpoint lives in `Jigen.csproj`.
- Verification: 10 Python tests passed (`pytest src/Client/Jigen.Python/tests`), up from 5, covering multi-vector mapping, mixed-shape rejection and `get_embeddings`.
- Verification: `check-python-multivector.py` reported OK against the live server, and the async path returned all vectors.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY

Status: Completed.

- Removed the single-vector ambiguity end to end: `VectorEntry.Embedding`, `VectorEntry<T>.Embedding`, `IStore.GetEmbedding`, `VectorCollection.GetEmbedding`, `Store.GetEmbedding`, the REST route `/embedding`, the CQRS query `GetEmbedding` and the gRPC RPC `GetEmbedding` no longer exist.
- `VectorEntry.Embeddings` is now the only vector payload at every layer; a single-vector entry is a list of one, and the store, WAL, transaction, shrink and indexers read that list.
- `SmallWorldIndexer.AddToIndex` expands one entry into one node per vector, so callers no longer fan out per vector; `WriterThread` and `ReconcileAsync` pass whole entries.
- Wire contract: `Vector` keeps only `Vectors` (field 5 reserved), the stored-vector reply is the new `StoredEmbeddingsResponse { repeated EmbeddingVector Vectors }`, and `EmbeddingResponse` keeps its single `Embeddings` because it belongs to the embedding service (one vector per sentence).
- REST: `VectorPayload`, `BulkVectorItem` and the transaction payloads carry only `Vectors`; a payload without vectors is rejected with 400; the read route is `.../documents/{key}/embeddings`.
- Clients: .NET `Add(key, content, float[])` wraps the vector into a one-element list, `GetEmbeddings/GetEmbeddingsAsync` are the only readers; Python regenerated bindings, `embedding=` always fills `Vectors`, `get_embeddings` is the only reader.
- Verification: `Jigen.Primitives`, `Jigen.Store`, `Jigen.Indexer.HNSW`, `Jigen.API`, `Jigen.Grpc`, both server hosts, `Jigen.Client` and all benchmark/test projects built with 0 errors (only the pre-existing `EmbeddingControllerTests` constructor error remains).
- Verification: `JigenStoreTests` 64 passed, `HnswTest` 20 passed, Python 10 passed.
- Verification: live server — the Python contract script reported OK, the REST upsert returned 2 vectors from `.../embeddings`, a payload without vectors got 400, and the .NET client read back `[[1, 0]]` and `[[1, 0], [0, 1]]`.
- Verification: macro-benchmark smoke run at 5k×64 reproduced recall 0.998.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY-GUARD

Status: Completed.

- The vector RPCs (`SetVector`, `SetVectors`, `AppendVector`, `SetRawVector`) now reject a request whose `Vectors` list is empty with `InvalidArgument`, so a client still sending the removed single-vector field fails loudly instead of storing an entry without vectors.
- REST already rejected the same case with 400.
- Verification: `Jigen.csproj` built with 0 errors; documented in `docs/server/grpc-api.md`.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-EMBEDDINGS-ONLY-BENCHMARK

Status: Completed.

- Re-ran the macro-benchmark after the embeddings-only refactor to confirm no runtime cost: single-vector ingest 1904–2059 docs/s (was 1838), HNSW search 559–602 µs (was 745), recall 0.993 (was 0.994), graph and vectors file sizes unchanged.
- Multi-vector unchanged within run-to-run variance: K=4 ingest 366–388 docs/s (was 372), recall 0.895–0.900 / 0.926–0.932 on a non-first vector (was 0.902 / 0.935); K=8 ingest 171 docs/s, recall 0.815 / 0.831.
- Lazy indexing unchanged in shape: K=1 ingest 104,644 docs/s then a 14.54 s build; K=4 91,237 docs/s then 102.22 s.
- 100k single-vector run: ingest 1186 docs/s, search 1153 µs, recall 0.807, reopen 343 ms (previous run 1048, 1349 µs, 0.798, 444 ms).
- Comparative suite against Qdrant 1.19.2 at `random-10k` still runs: Jigen HNSW w8 4,033 vec/s and 0.18 ms post-index P50 versus Qdrant 28,066 vec/s and 2.73 ms, both recall 1.000.
- `docs/benchmarks.md` updated with the re-measured multi-vector and lazy tables.
- Activated skills: `workflow-development`, `phase-development-project-conventions`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.
