# Embeddings

Jigen generates text embeddings with an ONNX Runtime pipeline built around the [`nomic-embed-text-v1.5`](https://huggingface.co/nomic-ai/nomic-embed-text-v1.5) model family. This page describes how the pipeline works, where it can run, and the API surface exposed to callers.

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
