# Development Dossier

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

## DEV-PASSAGE-EMBEDDING-CONTRACT

Status: Production code verified; awaiting code-quality approval before the separate test task.

- Added `CalculatePassageEmbeddings` with model, task, text, passage-size and overlap parameters.
- Added the structured `PassageEmbedding` result with text, start token, token count and embedding.
- Added token-aware passage splitting at word boundaries and batch generation of one vector per passage.
- Existing single-vector and batch contracts remain unchanged. Store and HNSW are not involved yet.
- Verification: `Jigen.Embedding.Handlers` and its dependencies built with 0 warnings and 0 errors.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-PASSAGE-EMBEDDING-TRANSPORTS

Status: Production code verified; awaiting code-quality approval before the separate test task.

- Added `POST /api/embeddings/passages` to both REST embedding hosts.
- Added the `CalculatePassageEmbeddings` unary gRPC operation and synchronized server/client protobuf contracts.
- Added synchronous and asynchronous passage helpers to `Jigen.Client`.
- REST and gRPC reject empty text, non-positive passage size, negative overlap and overlap greater than or equal to passage size.
- Store and HNSW remain unchanged and are not called by the new transport operations.
- Verification: both REST projects, the gRPC server and `Jigen.Client` built successfully with 0 errors. Existing generated-source, nullable and ONNX runtime warnings remain.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-PASSAGE-SERVER-LIMITS

Status: Production code verified; awaiting code-quality approval before the separate test task.

- Added centralized passage defaults (`128` tokens, `32` overlap) and server limits (`512` tokens, `128` overlap) under `JigenEmbeddings:PassageSplitting`.
- REST request sizes are nullable; omitted values use server defaults and explicit values are checked against server limits.
- gRPC passage size and overlap are optional scalar fields with presence tracking; the .NET client helpers accept nullable overrides.
- Startup validates the configured defaults and maximums before accepting requests.
- Verification: handlers, both REST projects, the gRPC server and `Jigen.Client` built with 0 errors. Existing protobuf generated-source, nullable and unused-import warnings remain.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.

## DEV-PASSAGE-SPLIT-COUNT

Status: Production code verified; awaiting code-quality approval before the separate test task.

- Added `CalculatePassageSplitCount`, which applies the same tokenizer-aware splitter as passage embedding without running model inference.
- Added `POST /api/embeddings/passages/count` to both REST hosts.
- Added `CalculatePassageSplitCount` to gRPC and synchronous/asynchronous helpers to `Jigen.Client`.
- Optional passage sizing uses the same server defaults and limits as passage embedding.
- Verification: handlers, both REST projects, the gRPC server and `Jigen.Client` built with 0 errors. Existing generated-source, nullable and unused protobuf import warnings remain.
- Activated skills: `workflow-development`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `base-be-base-rules`, and `implementation-be-dotnet-dev`.
