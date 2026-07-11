# Embeddings Configuration

Embedding generation is configured through the `JigenEmbeddings` configuration section, read by both the all-in-one server and the standalone `jigen-embeddings` worker. Settings can be overridden with the standard ASP.NET Core environment-variable convention (e.g. `JigenEmbeddings__EmbeddingsMaxConcurrency`).

## `JigenEmbeddings`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `TokenizerPath` | string | — (required) | Path to the ONNX tokenizer model (`tokenizer.onnx`), or to a `tokenizer.json` to use the SentencePiece code path instead (requires a sibling `sentencepiece.bpe.model` file). |
| `EmbeddingsModelPath` | string | — (required) | Path to the ONNX embedding model. |
| `GeneratorOptions` | `EmbeddingGeneratorOptions` | see below | Tokenization, chunking, batching, and execution provider settings, see next table. |
| `EmbeddingsMaxConcurrency` | int | `2` | Number of worker tasks draining the internal request queue; effectively the number of concurrent ONNX inference calls in flight. |
| `EmbeddingsQueueCapacity` | int | `256` | Maximum number of pending embedding requests buffered before new requests block on enqueue. |
| `EmbeddingsQueueTimeoutSeconds` | int | `60` | How long an enqueue attempt waits before failing with a `TimeoutException` when the queue is full. |
| `DefaultTask` | string | `null` | Task prefix used when a request does not specify one explicitly (`null` values fall back to this; an empty string does not). |
| `Tasks` | string[] | — | List of task names advertised by the `/api/embeddings/tasks` endpoint (e.g. `search_document`, `search_query`, `clustering`, `classification`). Informational — any string can still be passed as a task. |

## `EmbeddingGeneratorOptions`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `MaxTokens` | int | `384` | Maximum tokens per inference sequence; longer inputs are chunked or truncated (see below). Clamped to a minimum of 8. |
| `UseChunking` | bool | `true` | When `true`, inputs longer than `MaxTokens` are split into overlapping chunks and their embeddings combined by weighted average. When `false`, longer inputs are truncated with a head-tail strategy instead. |
| `ChunkSize` | int | `320` | Tokens per chunk when chunking is enabled. Clamped between 8 and `MaxTokens`. |
| `ChunkOverlap` | int | `64` | Tokens shared between consecutive chunks. Clamped between 0 and `ChunkSize - 1`. |
| `HeadTailHeadTokens` | int | `256` | Tokens kept from the start of the input when head-tail truncation is used; the remainder up to `MaxTokens` is filled from the end of the input. Clamped between 1 and `MaxTokens - 1`. |
| `IntraOpNumThreads` | int | `0` | ONNX Runtime intra-op thread count for a single inference run. `0` (or negative) lets ONNX Runtime pick automatically (all cores); server modules compute an explicit value of `max(1, ProcessorCount / EmbeddingsMaxConcurrency)` to avoid oversubscribing the CPU when multiple concurrent inference runs are active. |
| `MaxBatchSize` | int | `1` | Maximum number of token sequences fused into a single ONNX inference run. `1` disables batching. Batching is opt-in: on CPU, intra-op parallelism already saturates the cores and padding mixed-length inputs wastes compute, so batching rarely helps; it is intended for GPU execution providers. |
| `ExecutionProvider` | string | `"cpu"` | ONNX Runtime execution provider. See [execution providers](execution-providers.md) for the full list and build requirements. |
| `GpuDeviceId` | int | `0` | Device index used by GPU execution providers (`cuda`, `dml`, `rocm`, `migraphx`). |

## Model files layout

Model and tokenizer files are expected under `/data/onnx/<model-name>/` in the `jigendb-all-in-one` and `jigen-embeddings` Docker images, e.g. for the default model:

```
/data/onnx/nomic-embed-text-v1.5/
├── tokenizer.onnx
└── model_int8.onnx
```

`TokenizerPath` and `EmbeddingsModelPath` point at these two files. `model_int8.onnx` is the recommended variant on CPU (roughly 2.4× faster than fp32 in Jigen's benchmarks, with identical retrieval ranking observed in testing — see [benchmarks](../benchmarks.md)); an fp16 model variant is recommended instead when running on a GPU execution provider.

## Example: production-style configuration

```json
{
  "JigenEmbeddings": {
    "TokenizerPath": "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx",
    "EmbeddingsModelPath": "/data/onnx/nomic-embed-text-v1.5/model_int8.onnx",
    "GeneratorOptions": {
      "MaxTokens": 2048,
      "UseChunking": true,
      "ChunkSize": 1536,
      "ChunkOverlap": 192,
      "HeadTailHeadTokens": 4096
    },
    "EmbeddingsMaxConcurrency": 2,
    "EmbeddingsQueueCapacity": 10,
    "EmbeddingsQueueTimeoutSeconds": 60,
    "DefaultTask": "search_document",
    "Tasks": [
      "search_document",
      "search_query",
      "clustering",
      "classification"
    ]
  }
}
```

This example is taken from the shipped `jigen-embeddings` worker configuration. Note that `EmbeddingsQueueCapacity` here (`10`) is deliberately lower than the library default (`256`), and `HeadTailHeadTokens` (`4096`) exceeds `MaxTokens` — since chunking is enabled in this example, `HeadTailHeadTokens` is unused (it only applies to the non-chunking truncation path).
