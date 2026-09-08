# Cross-modal search: comparability, modality gap and calibration

`nomic-embed-text-v1.5` and `nomic-embed-vision-v1.5` share the **same embedding space** (768-dim), so an image and the text that describes it land close together. That does **not** mean their raw similarity scores are comparable. This page explains why, and how Jigen lets you build a cross-modal search whose ranking is actually meaningful — with real numbers measured on the nomic models.

## The problem: scores are not comparable across modalities

Say you search a mixed collection (images + texts) with the query *"a tabby cat sitting and looking at the camera"*. What you get, with raw cosine similarities, looks like this:

| rank | type  | entry                                             | cosine  |
|-----:|-------|---------------------------------------------------|--------:|
| 0    | text  | a tabby cat sitting and looking at the camera     | 0.9050  |
| 1    | text  | a red Ferrari sports car parked on the street     | 0.4742  |
| 2    | text  | a tropical beach in Bali with palm trees ...      | 0.4557  |
| 3    | text  | a plate of spaghetti alla carbonara ...           | 0.4396  |
| 4    | text  | a golden retriever dog                            | 0.4315  |
| 5    | image | cat-tabby.jpg                                     | 0.0968  |
| ...  | image | ferrari-red-car.jpg, spaghetti-carbonara.jpg ...  | 0.02–0.06 |

The **matching image is correct** — it is the first image — but it is buried under *all* the texts, and the raw numbers look like the image is a terrible match. It is not. The cosine scales simply differ:

| pair           | mean cosine ± std |
|----------------|------------------:|
| text ↔ text    | 0.554 ± 0.029     |
| image ↔ image  | 0.729 ± 0.034     |
| **image ↔ text** | **0.034 ± 0.024** |

Text↔text similarity is **~16×** higher than image↔text. This is not a bug in the models: it is the **modality gap**, a well-known property of contrastive multimodal models (CLIP-style, including nomic). The image and text clusters live in the *same* space but are separated by a roughly constant offset (see Liang et al., *"Mind the Gap"*, 2022). Within each modality the similarities behave normally; across modalities they are compressed to a low, narrow range.

The consequence: **raw scores are only comparable within a modality.** Comparing a raw image score to a raw text score is like comparing temperatures in Celsius and Fahrenheit — the ordering inside each scale is fine, the numbers across scales are not.

## Two ways to restore comparability

Jigen ships two complementary calibrators in the `Jigen.Calibration` package (used by both the in-process engine and the .NET client):

| | `ModalityGapCalibrator` | `CrossModalScoreCalibrator` |
|---|---|---|
| What it changes | The **vectors** (embedding space) | The **scores** (ranking space) |
| When to apply | **Index time** (once, offline) | **Query time** (per search) |
| Result | Cross-modal cosines rise to the within-modal scale | A single ranking where each modality's "best" competes fairly |
| Needed for a good ranking | No | Yes |
| Needed to display comparable "similarity" values | Yes | No |

### 1. Modality gap correction (embedding space, index time)

The gap is estimated from a reference set — typically the collection itself — as the difference between the mean image vector and the mean text vector:

```
gap = mean(image embeddings) − mean(text embeddings)
```

`CorrectImage(v)` subtracts the gap and re-normalizes, moving the image cluster onto the text cluster:

```csharp
using Jigen.Calibration;

var calibrator = new ModalityGapCalibrator(imageVectors, textVectors);

// Correct at INDEX time, before storing the image vectors in Jigen.
var corrected = calibrator.CorrectImage(imageVector);
```

Measured with nomic on a 5-image dataset: image↔text mean cosine goes from **0.034 → 0.694** (a ~20× lift, now on the text↔text scale), while every image stays closest to its *own* description — the alignment the model was trained for survives the correction.

> Use this when you want **one unified collection** where image and text vectors are stored side by side and raw scores are directly meaningful (e.g. to display "similarity: 0.69" in a UI).

### 2. Z-score calibration (score space, query time)

Instead of fixing the vectors, you can fix the *ranking*. For each modality group of the candidate set, the scores are standardized to mean 0, std 1:

$$z_i = \frac{x_i - \mu_{\text{group}}}{\sigma_{\text{group}}}$$

with $\mu$ and $\sigma$ computed **only over the candidates of that group**. A z-score answers the right ranking question: *"how much does this candidate stand out within its own modality?"* — not *"is its raw cosine high in absolute terms?"*. Applying it to the query above (groups = image/text):

| rank | type  | entry         | raw    | z      |
|-----:|-------|---------------|-------:|-------:|
| 0    | text  | *own text*    | 0.9050 | **+1.99** |
| 1    | image | *own image*   | 0.0968 | **+1.59** |
| 2    | text  | Ferrari text  | 0.4742 | −0.36  |
| ...  |       | ...           | ...    | ...    |

The own text and the own image now rank #1 and #2 — both are the "best of their group", which is the correct cross-modal answer.

**Why z-score works — and what it does not do:**

- It is **monotonic per group**: $(x - \mu)/\sigma$ grows with $x$, so the internal ordering of each collection is untouched.
- It **removes the scale difference**: "+2" in the text group and "+2" in the image group mean the same thing — "exceptionally good relative to my own modality".
- It is **query-dependent**: $\mu$ and $\sigma$ come from the current candidate set, so it is a *ranking* calibrator, not an absolute metric. If the collection grows, the numbers change.
- It does **not** touch the stored vectors, and it does not make raw cosines comparable — use the gap correction for that.

## Which architecture: one collection or many?

### Option A — separate collections (recommended for retrieval)

Keep images and texts in different collections, search each with its own tuning, then merge with the z-score calibration:

```
image collection  ── search (efSearch for recall) ──► image hits (raw cosine, low scale)
text  collection  ── search (efSearch for recall) ──► text  hits (raw cosine, high scale)
                                                          │
                            CrossModalScoreCalibrator (group = collection)
                                                          ▼
                                          merged ranking (z-scores, comparable)
```

Two things to know:

1. **`EfSearch` does not calibrate scores.** It is the HNSW beam width (recall vs latency) and never changes the returned scores — measured: identical top-1 cosine with `EfSearch = 8` and `EfSearch = 64`. Tune it per collection for *recall*, not for comparability.
2. **The merge still needs the z-score.** Even with clean per-collection rankings, merging by raw score buries the image at #6 again (0.097 vs 0.905). Z-score per collection puts the own text at #1 and the own image at #2, **without** touching the vectors.

### Option B — single unified collection (needs gap correction)

Store image and text vectors side by side after correcting the image side at index time (`ModalityGapCalibrator`). Raw scores become comparable and search works out of the box, but every image must be corrected before insertion — and the gap estimate should come from a representative reference set.

### Summary

| | Separate collections | Single collection |
|---|---|---|
| Vectors | original | image side gap-corrected at index time |
| Query-time step | z-score merge per collection | none (scores already comparable) |
| efSearch | per collection (recall tuning) | single collection default |
| Best for | text-first or image-first retrieval, large collections | mixed browsing, comparable similarity values in UI |

## Using it from the .NET client

### One-liner: `MergeAndCalibrate` (extension method)

`Jigen.Client` exposes `MergeAndCalibrate` as an extension method on
`IEnumerable<VectorSearchResult<T>>`: every result set passed is treated as one
collection/modality, z-scored independently, and merged into a single ranking:

```csharp
using Jigen.Client;

// 1. Search each collection separately, with its own tuning.
var imageHits = images.Search(query, top: 5, options: new SearchOptions { EfSearch = 64 });
var textHits  = texts .Search(query, top: 5, options: new SearchOptions { EfSearch = 8 });

// 2. Merge on a common scale — no labels needed: each IEnumerable is one group.
var merged = imageHits.MergeAndCalibrate(textHits);

foreach (var hit in merged)  // ordered by CalibratedScore (descending)
{
  Console.WriteLine($"{hit.Modality,-6} {hit.RawScore:F4}  z={hit.CalibratedScore:+0.00;-0.00}  {hit.Result.Content}");
}
```

Any number of groups works (`imageHits.MergeAndCalibrate(textHits, audioHits, ...)`);
`null` groups are skipped. `CalibratedSearchResult<T>.Modality` carries the
group's zero-based position among the non-null groups ("0" = the receiver, "1" = the first
argument, ...) — if you prefer meaningful labels, use the labelled variant below.

### Labelled: `CrossModalSearch.MergeCalibrated`

```csharp
var merged = CrossModalSearch.MergeCalibrated(
    ("image", imageHits),
    ("text",  textHits));
```

Both variants return `CalibratedSearchResult<T>`, which carries:

- `Modality` — the group label/position you passed in;
- `RawScore` — the score returned by the server (only comparable within the modality);
- `CalibratedScore` — the z-score of `RawScore` within its group (the field the merge is ordered by).

Groups with a single hit or zero variance map to `CalibratedScore = 0` (no ranking signal within the group).

For the single-collection architecture, correct the vectors before uploading:

```csharp
using Jigen.Calibration;

// Collect a reference sample (e.g. 100 images + their descriptions).
var calibrator = new ModalityGapCalibrator(sampleImageVectors, sampleTextVectors);

// Index time: correct every image vector, store as usual.
collection.Add(imageKey, imageContent, calibrator.CorrectImage(imageVector));
collection.Add(textKey,  textContent,  textVector);   // texts stay as they are
```

## Where the calibration code lives

| Assembly | Contents |
|---|---|
| `Jigen.Calibration` (net8.0 + net10.0, no external dependencies) | `ModalityGapCalibrator`, `CrossModalScoreCalibrator` |
| `Jigen.Client` | `VectorSearchResultExtensions.MergeAndCalibrate`, `CrossModalSearch.MergeCalibrated`, `CalibratedSearchResult<T>` |
| `Jigen.SemanticTools` | the ONNX text/image embedding generators (`OnnxEmbeddingGenerator`, `OnnxImageEmbeddingGenerator`) |

The calibrators are pure math and deliberately dependency-free, so they can be referenced from both the net8.0 client and the net10.0 server/tools without pulling in ONNX Runtime.

## See also

- [Embeddings overview](overview.md) — the nomic text/vision pipelines and why the two models share a space
- [HNSW](../indexes/hnsw.md) — what `EfSearch` really controls (beam width, recall vs latency)
- [Client usage](../client/usage.md) — `SearchOptions`, per-query tuning, precomputed vectors
