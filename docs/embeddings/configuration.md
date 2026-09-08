# Embeddings Configuration

Embedding generation is configured through the `JigenEmbeddings` configuration section, read by both the all-in-one server and the standalone `jigen-embeddings` worker. Settings can be overridden with the standard ASP.NET Core environment-variable convention (e.g. `JigenEmbeddings__EmbeddingsMaxConcurrency`).

## Multiple text models

The legacy single-model keys remain supported. To expose independent vector
spaces, configure named entries under `Models` and select one with `Model` in
the single/batch API or several with `POST /api/embeddings/multi`:

```json
{
  "JigenEmbeddings": {
    "DefaultModel": "qwen3",
    "Models": {
      "qwen3": {
        "TokenizerPath": "/data/onnx/qwen3-embedding-0.6b/tokenizer.onnx",
        "ModelPath": "/data/onnx/qwen3-embedding-0.6b/model.onnx",
        "GeneratorOptions": {
          "Profile": "Qwen3",
          "MaxTokens": 8192,
          "UseChunking": false,
          "OutputDimension": 1024,
          "PaddingTokenId": 151643,
          "ExecutionProvider": "cuda",
          "MaxBatchSize": 8
        }
      },
      "siglip2": {
        "TokenizerPath": "/data/onnx/siglip2-base-patch16-224/tokenizer.onnx",
        "ModelPath": "/data/onnx/siglip2-base-patch16-224/text_encoder.onnx",
        "GeneratorOptions": {
          "Profile": "SigLip2",
          "MaxTokens": 64,
          "UseChunking": false,
          "PaddingTokenId": 1,
          "ExecutionProvider": "cuda",
          "MaxBatchSize": 8
        }
      }
    }
  }
}
```

Each model owns its queue settings (`MaxConcurrency`, `QueueCapacity`, and
`QueueTimeoutSeconds`). `Qwen3` uses left padding, last-token pooling and L2
normalization. `OutputDimension` truncates the native vector before L2
normalization for Matryoshka checkpoints. `SigLip2` lowercases input, pads to
`MaxTokens`, prefers projected outputs named `text_embeds`, `text_features` or
`pooler_output`, and applies L2 normalization. Nomic retains mean pooling plus
layer normalization and L2 normalization.

The multi-model endpoint returns a JSON object keyed by model name. It does not
combine vectors because vectors from different models are not interchangeable.
For Qwen3, leave `DefaultTask` empty when the same model embeds documents and
pass an English retrieval instruction only for query calls; the handler formats
it as `Instruct: ...\nQuery:...`.

For the paired SigLIP2 vision tower, configure `ImageGeneratorOptions:Profile`
as `SigLip2`, set `InputWidth`, `InputHeight`, `ImageMean` and `ImageStd` from
the checkpoint's `preprocessor_config.json`, and point `ImagesModelPath` to an
ONNX export exposing `image_embeds`, `image_features` or `pooler_output`.
Fixed-resolution checkpoints are supported; NaFlex exports requiring
`pixel_attention_mask` and `spatial_shapes` are not supported by this pipeline.

## `JigenEmbeddings`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `TokenizerPath` | string | — (required) | Path to the ONNX tokenizer model (`tokenizer.onnx`), or to a `tokenizer.json` to use the SentencePiece code path instead (requires a sibling `sentencepiece.bpe.model` file). |
| `EmbeddingsModelPath` | string | — (required) | Path to the ONNX embedding model. |
| `GeneratorOptions` | `EmbeddingGeneratorOptions` | see below | Tokenization, chunking, batching, and execution provider settings, see next table. |
| `EmbeddingsMaxConcurrency` | int | `2` | Number of worker tasks draining the internal request queue; effectively the number of concurrent ONNX inference calls in flight. |
| `EmbeddingsQueueCapacity` | int | `256` | Maximum number of pending embedding requests buffered before new requests block on enqueue. |
| `EmbeddingsQueueTimeoutSeconds` | int | `60` | How long an enqueue attempt waits before failing with a `TimeoutException` when the queue is full. |
| `DefaultTask` | string | `null` | Task/instruction used when a request does not specify one explicitly. Null and empty request values both fall back to this setting. |
| `Tasks` | string[] | — | List of task names advertised by the `/api/embeddings/tasks` endpoint (e.g. `search_document`, `search_query`, `clustering`, `classification`). Informational — any string can still be passed as a task. |

## `EmbeddingGeneratorOptions`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Profile` | enum | `Nomic` | Model contract: `Nomic`, `Qwen3`, `SigLip2`, or `Custom`. Controls padding, pooling, input formatting and normalization. |
| `MaxTokens` | int | `384` | Maximum tokens per inference sequence; longer inputs are chunked or truncated (see below). Clamped to a minimum of 8. |
| `UseChunking` | bool | `true` | When `true`, inputs longer than `MaxTokens` are split into overlapping chunks and their embeddings combined by weighted average. When `false`, Nomic uses head-tail truncation; Qwen3 and SigLIP2 use right truncation. |
| `ChunkSize` | int | `320` | Tokens per chunk when chunking is enabled. Clamped between 8 and `MaxTokens`. |
| `ChunkOverlap` | int | `64` | Tokens shared between consecutive chunks. Clamped between 0 and `ChunkSize - 1`. |
| `HeadTailHeadTokens` | int | `256` | Tokens kept from the start of the input when head-tail truncation is used; the remainder up to `MaxTokens` is filled from the end of the input. Clamped between 1 and `MaxTokens - 1`. |
| `IntraOpNumThreads` | int | `0` | ONNX Runtime intra-op thread count for a single inference run. `0` (or negative) lets ONNX Runtime pick automatically (all cores); server modules compute an explicit value of `max(1, ProcessorCount / EmbeddingsMaxConcurrency)` to avoid oversubscribing the CPU when multiple concurrent inference runs are active. |
| `MaxBatchSize` | int | `1` | Maximum number of token sequences fused into a single ONNX inference run. `1` disables batching. Batching is opt-in: on CPU, intra-op parallelism already saturates the cores and padding mixed-length inputs wastes compute, so batching rarely helps; it is intended for GPU execution providers. |
| `OutputDimension` | int | `0` | Output dimension for Matryoshka models. Zero keeps the native size; truncation is followed by L2 normalization. |
| `PaddingTokenId` | long | `0` | Token id written into padded positions; set it from the checkpoint tokenizer configuration. |
| `ExecutionProvider` | string | `"cpu"` | ONNX Runtime execution provider. See [execution providers](execution-providers.md) for the full list and build requirements. |
| `GpuDeviceId` | int | `0` | Device index used by GPU execution providers (`cuda`, `dml`, `rocm`, `migraphx`). |

## `ImageEmbeddingGeneratorOptions`

Options for the image embedding generator (`OnnxImageEmbeddingGenerator`, see [overview](overview.md#image-embeddings)). These are configured in the server through the `JigenEmbeddings:ImageGeneratorOptions` section (they bind directly to this class).

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Profile` | enum | `Nomic` | Vision output contract. Use `SigLip2` for a projected fixed-resolution SigLIP2 vision export. |
| `InputWidth` | int | `224` | Target width the input image is resized to before inference. |
| `InputHeight` | int | `224` | Target height the input image is resized to before inference. |
| `ImageMean` | float[3] | `[0.48145466, 0.4578275, 0.40821073]` | Per-channel RGB normalization mean (CLIP ImageNet values from `nomic-embed-vision-v1.5`'s `preprocessor_config.json`). |
| `ImageStd` | float[3] | `[0.26862954, 0.26130258, 0.27577711]` | Per-channel RGB normalization standard deviation (CLIP ImageNet values). |
| `IntraOpNumThreads` | int | `0` | ONNX Runtime intra-op thread count for a single inference run. `0` (or negative) lets ONNX Runtime pick automatically (all cores). |
| `MaxBatchSize` | int | `1` | Maximum number of images fused into a single ONNX inference run. `1` disables batching. On CPU the intra-op parallelism already saturates the cores; raise it (8–32) on GPU providers. |
| `TileColumns` | int | `4` | Number of tile columns for the tile grid used by `GenerateImageTileEmbeddings`. The number of rows is derived from the image aspect ratio, so each tile is square and the grid covers the image with no gaps. |
| `TileOverlap` | float | `0.2` | Overlap between adjacent tiles as a fraction of the tile size (`0` = none, `0.2` = 20%). Clamped between 0 and 0.9. |
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
    ],
    "ImagesModelPath": "/data/onnx/nomic-embed-vision-v1.5/model.onnx",
    "ImageGeneratorOptions": {
      "InputWidth": 224,
      "InputHeight": 224,
      "TileColumns": 4,
      "TileOverlap": 0.2
    },
    "ImageEmbeddingsMaxConcurrency": 2,
    "ImageEmbeddingsQueueCapacity": 10,
    "ImageEmbeddingsQueueTimeoutSeconds": 60
  }
}
```

The text part of this example is taken from the shipped `jigen-embeddings` worker configuration. Note that `EmbeddingsQueueCapacity` here (`10`) is deliberately lower than the library default (`256`), and `HeadTailHeadTokens` (`4096`) exceeds `MaxTokens` — since chunking is enabled in this example, `HeadTailHeadTokens` is unused (it only applies to the non-chunking truncation path). The image part is **opt-in**: setting `ImagesModelPath` to an empty string disables image embedding entirely (the server keeps running text-only and image requests fail with a clear configuration error).
