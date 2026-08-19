# Embeddings

Jigen generates text embeddings with an ONNX Runtime pipeline built around the [`nomic-embed-text-v1.5`](https://huggingface.co/nomic-ai/nomic-embed-text-v1.5) model family, and image embeddings with the aligned [`nomic-embed-vision-v1.5`](https://huggingface.co/nomic-ai/nomic-embed-vision-v1.5) model — which shares the **same embedding space**, so text and image vectors can be stored together in one collection and searched cross-modally. This page describes how the pipelines work, where they can run, and the API surface exposed to callers.

## Pipeline

Generating an embedding for a piece of text goes through the following stages:

1. **Tokenization.** The input text is tokenized by an ONNX tokenizer (`tokenizer.onnx`, or a SentencePiece model when a `tokenizer.json` is supplied instead). The tokenizer session runs single-threaded.
2. **Chunking (long inputs only).** If the token count exceeds `MaxTokens`:
   - When `UseChunking` is enabled (default), the token sequence is split into overlapping chunks of `ChunkSize` tokens with `ChunkOverlap` tokens shared between consecutive chunks. Each chunk is embedded independently and the resulting vectors are combined into a single vector using a token-count-weighted average.
   - When `UseChunking` is disabled, the input is truncated with a head-tail strategy: the first `HeadTailHeadTokens` tokens are kept, followed by the last remaining tokens up to `MaxTokens`.
3. **Batched model inference.** Token sequences (whole texts or chunks) are sorted by length and grouped into batches of up to `MaxBatchSize` sequences, padded to the longest sequence in the batch, and run through the ONNX embedding model in a single inference call per batch. Batching is fusion for throughput only — it does not change the resulting vectors.
4. **Vector extraction.** Jigen reads the model output in this order of preference: a `sentence_embedding` tensor if the model exposes one, otherwise any 2-D pooled output, otherwise a 3-D per-token hidden-state tensor which Jigen mean-pools internally over the valid (non-padding) tokens.
5. **Task prefix.** `nomic-embed-text-v1.5` expects a task instruction prefixed to the input, e.g. `search_document: <text>` or `search_query: <text>`. Jigen prepends `"{task}: "` when a task is supplied. Standard tasks are `search_document`, `search_query`, `clustering`, `classification`.

## Where embeddings run

Embedding generation is not tied to a single process. The same `Jigen.SemanticTools` engine (`OnnxEmbeddingGenerator`) is used in every case; only where it is hosted changes:

| Mode | Description |
|---|---|
| In-process (all-in-one server) | The `ppossanzini/jigendb-all-in-one` server image loads the ONNX tokenizer and model directly and computes embeddings in the same process that serves gRPC/REST requests. No RabbitMQ needed. |
| Dedicated worker | The `ppossanzini/jigen-embeddings` image runs the same embedding engine as a standalone worker, consuming embedding requests from RabbitMQ (via the Hikyaku/Kaido mediator) so multiple workers can be scaled independently of the database server. It also exposes its own REST endpoint, `/api/embeddings`. See [server overview](../server/overview.md) and [docker](../server/docker.md) for the deployment topologies. |
| Client-side | Because collections accept a raw `float[]` vector (`SetVector` / the client's `Add(key, content, embeddings)` overload), any embedding generator — including a custom one, unrelated to `Jigen.SemanticTools` — can be used on the caller's side, with the vector handed to Jigen as-is. See [client usage](../client/usage.md). |

For details on server-side configuration of the embedding module, see [server configuration](../server/configuration.md).

## Queued generator

Server-hosted deployments (in-process or dedicated worker) wrap the raw `OnnxEmbeddingGenerator` in a `QueuedEmbeddingGenerator`, which adds:

- A bounded request queue (`EmbeddingsQueueCapacity`), so a burst of concurrent requests does not spawn unbounded ONNX inference calls.
- A fixed number of worker tasks (`EmbeddingsMaxConcurrency`) draining the queue; each worker coalesces up to `MaxBatchSize` already-queued requests into a single fused inference run before handing results back to their callers.
- An enqueue timeout (`EmbeddingsQueueTimeoutSeconds`): a request that cannot be placed on the queue within this time fails with a `TimeoutException` instead of blocking indefinitely.

See [configuration](configuration.md) for the full settings reference.

## API surface

The embedding generator is exposed through `IEmbeddingGenerator`:

```csharp
public interface IEmbeddingGenerator
{
  float[] GenerateEmbedding(string input);
  float[] GenerateEmbedding(string task, string input);
  float[][] GenerateEmbeddings(IReadOnlyList<string> inputs);

  Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
  Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default);
  Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}
```

Both synchronous and asynchronous overloads are available, with and without an explicit task prefix, for a single input or a batch of inputs; all asynchronous overloads accept a `CancellationToken`. When requests are routed through a `QueuedEmbeddingGenerator`, cancelling the token unblocks the caller immediately even if the request has not been picked up by a worker yet.

## Image embeddings

`nomic-embed-vision-v1.5` is a ViT (patch 16, 224×224, 768-dim output, ~93M params) trained to project images into the embedding space of `nomic-embed-text-v1.5` — the text encoder is kept frozen during alignment (LiT-style), which is why the two models are directly comparable. This makes image and text vectors interoperable: an image and a sentence that describe the same thing land close together, so a text query can find images and an image query can find texts in the same Jigen collection.

> **Why this model and not CLIP/SigLIP?** CLIP, SigLIP, Jina CLIP, etc. produce vectors in *different* embedding spaces: even at the same dimensionality, cosine similarity between a CLIP vector and a nomic vector is meaningless. If your text side stays on `nomic-embed-text-v1.5`, `nomic-embed-vision-v1.5` is the only model trained to share its space. The only way to use a different vision model is to change the text model too (to a unified multimodal family such as Jina CLIP v2 or SigLIP), which requires re-embedding existing data.

### Pipeline

Generating an embedding for an image goes through the following stages:

1. **Decode.** The image is decoded with ImageSharp (`Rgba32`), so all common formats are supported (PNG, JPEG, WebP, BMP, GIF, TIFF...).
2. **Resize.** The image is resized to `InputWidth` × `InputHeight` (default 224×224) with bicubic resampling. CLIP-style, the resize already produces the target size, so the model's center-crop step is a no-op.
3. **Rescale + normalize.** Pixel values are scaled to `[0, 1]` and normalized per channel with `ImageMean`/`ImageStd`, which default to the CLIP ImageNet values used by the model's own `preprocessor_config.json` (`mean = [0.48145466, 0.4578275, 0.40821073]`, `std = [0.26862954, 0.26130258, 0.27577711]`).
4. **Batched inference.** The preprocessed images are packed into an NCHW tensor `[B, 3, H, W]` and run through the ONNX vision model, in batches of up to `MaxBatchSize`.
5. **Vector extraction.** Jigen reads the output in this order of preference: a `last_hidden_state` tensor (the CLS token at index 0 is taken, matching the reference usage `F.normalize(img_emb[:, 0])`), otherwise any pooled 2-D output, otherwise — batch size 1 only — any remaining float tensor. The resulting vector is **L2-normalized**, as required for cosine similarity against the text side.

> **Task prefixes.** Images need no prefix, but the *text* side does: use `search_query:` for queries and `search_document:` for documents, exactly as with text-only retrieval.

### API surface

Image embeddings are exposed through `IImageEmbeddingGenerator`:

```csharp
public interface IImageEmbeddingGenerator
{
  float[] GenerateImageEmbedding(string imagePath);
  float[] GenerateImageEmbedding(byte[] imageBytes);
  float[][] GenerateImageEmbeddings(IReadOnlyList<byte[]> images);

  Task<float[]> GenerateImageEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default);
  Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
  Task<float[][]> GenerateImageEmbeddingsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default);
}
```

Both synchronous and asynchronous overloads accept a file path or in-memory bytes, for a single image or a batch.

### Usage example

```csharp
using Jigen.SemanticTools;

// 1. Load the vision model (exported from nomic-embed-vision-v1.5 to ONNX).
using var imageGenerator = new OnnxImageEmbeddingGenerator(
  "/data/onnx/nomic-embed-vision-v1.5/model.onnx",
  logger: null,
  options: new ImageEmbeddingGeneratorOptions
  {
    MaxBatchSize = 8      // raise on GPU; keep 1 on CPU
  });

// 2. Embed images (from file or bytes).
var imageVector = imageGenerator.GenerateImageEmbedding("/path/to/photo.jpg");
var batch = imageGenerator.GenerateImageEmbeddings([File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png")]);

// 3. Store image and text vectors side by side in the same Jigen collection —
//    they share the embedding space, so search works across modalities.
var textGenerator = new OnnxEmbeddingGenerator(
  "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx",
  "/data/onnx/nomic-embed-text-v1.5/model.onnx");

collection.Add(1, "a photo of a cat", textGenerator.GenerateEmbedding("search_document", "a photo of a cat"));
collection.Add(2, imageVector);  // image content

// 4. Cross-modal search: a text query retrieves the matching image.
var results = collection.Search(textGenerator.GenerateEmbedding("search_query", "un gatto"), top: 10);
```

Because collections accept raw `float[]` vectors, any of the deployment modes below work for images too.

### Where image embeddings run

Image embedding generation is available wherever the `Jigen.SemanticTools` engine is used:

| Mode | Description |
|---|---|
| In-process / client-side | The `OnnxImageEmbeddingGenerator` is used directly in your application; the resulting vector is handed to Jigen as-is. This is the current integration point — the server embedding module is text-only, see below. |
| Server (future) | The server embedding module (`JigenEmbeddings`) currently serves the text model only. Wiring the image model through the same module (config section + REST/gRPC endpoints + `QueuedEmbeddingGenerator` wrapper) is the natural next step. |

### Image model export

`nomic-embed-vision-v1.5` is not shipped as ONNX by default: export it with `optimum` (or `transformers` + `torch.onnx`), keeping the input name `pixel_values` and the output name `last_hidden_state` (Jigen resolves both from the session metadata, so non-standard names also work):

```bash
pip install optimum[exporters]
optimum-cli export onnx --model nomic-ai/nomic-embed-vision-v1.5 nomic-embed-vision-v1.5/
```

## See also

- [Configuration](configuration.md) — full `EmbeddingGeneratorOptions` and `ImageEmbeddingGeneratorOptions` reference
- [Execution providers](execution-providers.md) — CPU/GPU provider selection, shared by text and image engines
