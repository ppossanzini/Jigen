# Multi-model embeddings and federated search

Jigen can run multiple named embedding models without changing the vector
store. Each model produces an independent vector space; the application keeps
those spaces in separate collections (or databases), embeds the query once per
space, searches each collection, and reconciles the returned scores after the
search.

This guide uses three representative profiles:

- **Nomic** for general-purpose text retrieval and Nomic text/image alignment;
- **Qwen3 Embedding** for instruction-aware multilingual text retrieval and
  optional Matryoshka dimensions;
- **SigLIP2** for text/image retrieval with a paired vision encoder.

Model weights and ONNX exports are deployment assets and are not bundled with
Jigen. Verify every export against the input/output contract described below.

## The rule: one query embedding per vector space

Vectors from different model families are not interchangeable, even when they
have the same number of dimensions. A Qwen3 query vector cannot search a
SigLIP2 collection meaningfully, and a Nomic vector is not a conversion bridge
between them.

For a query spanning three spaces, the flow is therefore:

```text
                         ┌─ embed with qwen3  ─ search qwen3-text ─┐
user query ─ fan out ────┼─ embed with siglip2 ─ search siglip2   ├─ z-score per result set ─ merge
                         └─ embed with nomic  ─ search nomic-text ─┘
```

The query is embedded multiple times because each encoder defines its own
coordinate system. Jigen's multi-model REST endpoint performs this fan-out for
one text and returns a dictionary keyed by model name. It does not merge the
vectors or alter storage.

## Configure named models

```json
{
  "JigenEmbeddings": {
    "DefaultModel": "qwen3",
    "Models": {
      "qwen3": {
        "TokenizerPath": "/data/onnx/qwen3-embedding-0.6b/tokenizer.onnx",
        "ModelPath": "/data/onnx/qwen3-embedding-0.6b/model.onnx",
        "DefaultTask": null,
        "MaxConcurrency": 2,
        "QueueCapacity": 128,
        "QueueTimeoutSeconds": 60,
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
      },
      "nomic": {
        "TokenizerPath": "/data/onnx/nomic-embed-text-v1.5/tokenizer.onnx",
        "ModelPath": "/data/onnx/nomic-embed-text-v1.5/model_int8.onnx",
        "DefaultTask": "search_document",
        "GeneratorOptions": {
          "Profile": "Nomic",
          "MaxTokens": 2048,
          "UseChunking": true,
          "ChunkSize": 1536,
          "ChunkOverlap": 192
        }
      }
    }
  }
}
```

Names such as `qwen3` are application identifiers. They are case-insensitive,
must be unique, and are the values supplied in API requests. The legacy
single-model configuration remains valid; named `Models` are needed only when
one process exposes multiple spaces.

### ONNX contracts to verify

| Profile | Padding and pooling | Output handling | Important checks |
|---|---|---|---|
| `Nomic` | right padding, attention-mask mean pooling | layer normalization, then L2 normalization | tokenizer special tokens and exported hidden-state output |
| `Qwen3` | left padding, last non-padding token | optional `OutputDimension` truncation, then L2 normalization | EOS/pad IDs, maximum context, checkpoint Matryoshka support |
| `SigLip2` | fixed text length, profile-specific preprocessing | prefers `text_embeds`, `text_features`, or `pooler_output`, then L2 normalization | text and vision exports must be the paired checkpoint |

Do not infer compatibility from dimensionality alone. At startup, validate the
actual ONNX input names, element types, output shape, padding token and image
preprocessor configuration against the source checkpoint.

## Embed one text in several spaces

### All-in-one REST API

```bash
curl -X POST http://localhost:13223/api/embeddings/multi \
  -H "Authorization: Bearer $JIGEN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "a red bicycle beside a brick wall",
    "task": "Given a search query, retrieve relevant documents",
    "models": ["qwen3", "siglip2", "nomic"]
  }'
```

The response keeps the spaces explicit:

```json
{
  "qwen3": [0.012, -0.031, 0.044],
  "siglip2": [-0.087, 0.014, 0.006],
  "nomic": [0.021, 0.018, -0.055]
}
```

The arrays above are abbreviated. Their real lengths are determined by the
configured checkpoints and `OutputDimension`.

### Standalone embedding API

The standalone service uses the same concept with a slightly different body:

```bash
curl -X POST http://localhost:8080/api/embeddings/calculate/multi \
  -H "Content-Type: application/json" \
  -d '{
    "text": "a red bicycle beside a brick wall",
    "task": "Given a search query, retrieve relevant documents",
    "models": ["qwen3", "siglip2"]
  }'
```

For a single model, select it through `model`:

```bash
curl -X POST "http://localhost:8080/api/embeddings/calculate?model=qwen3" \
  -H "Content-Type: application/json" \
  -d '"a red bicycle beside a brick wall"'
```

### .NET client and gRPC

The typed client exposes model selection on the single and batch gRPC calls:

```csharp
var qwenQuery = await context.CalculateEmbeddingsAsync(
    sentence: query,
    task: "Given a search query, retrieve relevant documents",
    model: "qwen3");

var siglipQuery = await context.CalculateEmbeddingsAsync(
    sentence: query,
    model: "siglip2");

var qwenDocuments = await context.CalculateEmbeddingsBatchAsync(
    sentences: documents,
    model: "qwen3");
```

The multi-model fan-out endpoint is REST-only. With gRPC, call
`CalculateEmbeddingsAsync` once per model, optionally in parallel:

```csharp
var requests = new Dictionary<string, Task<float[]>>
{
    ["qwen3"] = context.CalculateEmbeddingsAsync(query, qwenInstruction, "qwen3"),
    ["siglip2"] = context.CalculateEmbeddingsAsync(query, model: "siglip2")
};

await Task.WhenAll(requests.Values);
var queryVectors = requests.ToDictionary(x => x.Key, x => x.Value.Result);
```

Keep concurrency bounded in production. Each named model owns a queue,
capacity and concurrency limit, but loading several large sessions still
consumes cumulative CPU/GPU memory.

## Indexing conventions

Every document must be embedded with the same model, profile, checkpoint,
instruction convention and output dimension later used for its query.

```csharp
var qwenDocumentVector = await context.CalculateEmbeddingsAsync(
    document.Text,
    model: "qwen3");

qwenCollection.Add(
    document.Id,
    new SearchDocument(document.Id, document.Text, "qwen3"),
    qwenDocumentVector);
```

Recommended metadata fields include a stable logical document ID, model name,
checkpoint revision, dimension, content type and source revision. These make
re-indexing and application-side deduplication deterministic.

Changing `OutputDimension`, checkpoint weights, pooling or query/document
formatting creates a new space. Treat it as an index migration: build a new
collection, validate it, switch traffic, then retire the old one.

## Search, calibrate, group and select

Search each collection with its matching query vector. Raw similarities from
different spaces are not directly comparable, so standardize each returned
candidate set independently and merge on `CalibratedScore`:

```csharp
var qwenHits = qwenCollection.Search(queryVectors["qwen3"], top: 40);
var siglipHits = siglipCollection.Search(queryVectors["siglip2"], top: 40);

var calibrated = CrossModalSearch.MergeCalibrated(
    ("qwen3", qwenHits),
    ("siglip2", siglipHits));
```

No additional post-search helper is required. The existing z-score calibration
provides score reconciliation; business rules remain normal LINQ. For example,
if both spaces return the same logical document:

```csharp
var finalHits = calibrated
    .GroupBy(hit => hit.Result.Content.DocumentId)
    .Select(group => group
        .OrderByDescending(hit => hit.CalibratedScore)
        .First())
    .OrderByDescending(hit => hit.CalibratedScore)
    .Take(10)
    .ToList();
```

Use `DistinctBy` when “first occurrence wins” is sufficient:

```csharp
var finalHits = calibrated
    .DistinctBy(hit => hit.Result.Content.DocumentId)
    .Take(10)
    .ToList();
```

The merge is already ordered by descending calibrated score, so this retains
the strongest representative. Group by stable metadata, not by vector values
or display titles.

### Candidate depth matters

Z-score statistics come from each query's candidate set. Retrieve more than the
final requested count (for example 30–100 candidates per space for a final top
10), use comparable candidate depths where practical, and measure ranking
stability on evaluation queries. A group with one result or zero variance has
no within-group ranking signal and receives a calibrated score of zero.

Z-score does not turn the calibrated value into an absolute probability. Use
it for cross-group ranking, while retaining `RawScore`, model and collection
metadata for diagnostics.

## Qwen3 query instructions

For the `Qwen3` profile, a non-empty task is formatted as:

```text
Instruct: {task}
Query:{input}
```

Embed documents without this query instruction; apply it only to queries. Keep
the instruction stable between evaluation and production. An instruction
changes retrieval behavior, so changing it is a ranking change even though the
checkpoint and vector dimension remain the same.

## SigLIP2 text/image setup

Use text and vision towers exported from the same SigLIP2 checkpoint. Configure
the vision side with `Profile: SigLip2`, the checkpoint's fixed resolution,
`ImageMean`, and `ImageStd`. Jigen supports projected fixed-resolution exports.
NaFlex exports that require `pixel_attention_mask` and `spatial_shapes` are not
supported by the current image pipeline.

```json
{
  "JigenEmbeddings": {
    "ImagesModelPath": "/data/onnx/siglip2-base-patch16-224/vision_encoder.onnx",
    "ImageGeneratorOptions": {
      "Profile": "SigLip2",
      "InputWidth": 224,
      "InputHeight": 224,
      "ImageMean": [0.5, 0.5, 0.5],
      "ImageStd": [0.5, 0.5, 0.5],
      "ExecutionProvider": "cuda",
      "MaxBatchSize": 8
    }
  }
}
```

The numeric preprocessing values are examples: copy the exact values from the
checkpoint's `preprocessor_config.json`.

## Operational checklist

- Pin model and tokenizer revisions; deploy them as an atomic pair.
- Record model, dimension and revision in collection metadata.
- Warm each configured model before accepting latency-sensitive traffic.
- Bound parallel fan-out according to available CPU/GPU memory.
- Reject unknown model names rather than silently falling back.
- Use the paired SigLIP2 text and vision exports.
- Re-index when checkpoint, pooling, normalization or output dimension changes.
- Retrieve a useful candidate pool before z-score calibration.
- Perform grouping and distinct selection after calibration with application LINQ.
- Evaluate recall and merged ranking separately; `EfSearch` affects ANN recall,
  not cross-space score comparability.

## Related documentation

- [Embedding configuration](configuration.md)
- [Cross-modal search and z-score calibration](cross-modal.md)
- [Execution providers](execution-providers.md)
- [REST API](../server/rest-api.md)
- [.NET client usage](../client/usage.md)
