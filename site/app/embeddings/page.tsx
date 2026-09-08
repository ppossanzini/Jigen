import type { Metadata } from "next";
import { CodeBlock, DetailShell } from "../_components/DetailShell";

export const metadata: Metadata = {
  title: "Multi-model embeddings — Jigen DB",
  description: "Run Nomic, Qwen3 and SigLIP2 embedding spaces independently, then reconcile search results with post-search z-score calibration.",
};

const config = `{
  "JigenEmbeddings": {
    "DefaultModel": "qwen3",
    "Models": {
      "qwen3": {
        "TokenizerPath": "/models/qwen3/tokenizer.onnx",
        "ModelPath": "/models/qwen3/model.onnx",
        "GeneratorOptions": {
          "Profile": "Qwen3",
          "OutputDimension": 1024
        }
      },
      "siglip2": {
        "TokenizerPath": "/models/siglip2/tokenizer.onnx",
        "ModelPath": "/models/siglip2/text_encoder.onnx",
        "GeneratorOptions": { "Profile": "SigLip2" }
      }
    }
  }
}`;

const query = `var jobs = new Dictionary<string, Task<float[]>> {
  ["qwen3"] = context.CalculateEmbeddingsAsync(
    query, retrievalInstruction, "qwen3"),
  ["siglip2"] = context.CalculateEmbeddingsAsync(
    query, model: "siglip2")
};

await Task.WhenAll(jobs.Values);

var qwenHits = qwen.Search(jobs["qwen3"].Result, top: 40);
var imageHits = images.Search(jobs["siglip2"].Result, top: 40);`;

const merge = `var ranked = CrossModalSearch.MergeCalibrated(
    ("qwen3", qwenHits),
    ("siglip2", imageHits));

var finalHits = ranked
    .GroupBy(x => x.Result.Content.DocumentId)
    .Select(g => g.OrderByDescending(x => x.CalibratedScore).First())
    .OrderByDescending(x => x.CalibratedScore)
    .Take(10)
    .ToList();`;

export default function EmbeddingsPage() {
  return (
    <DetailShell
      eyebrow="MULTI-MODEL RETRIEVAL"
      title="Different spaces. One ranked answer."
      intro="Jigen now hosts named Nomic, Qwen3 and SigLIP2 embedding pipelines without changing the vector store. Embed a query in every target space, search independently, then reconcile candidates after retrieval."
      index="05"
    >
      <section className="detailSection wrap">
        <div className="detailHeading"><span>01</span><div><h2>The invariant developers need.</h2><p>An embedding model defines a coordinate system. Equal dimensions do not make two model families compatible, so every searched space needs its own query vector.</p></div></div>
        <div className="embeddingFlow">
          <div className="flowSource"><small>INPUT</small><strong>ONE QUERY</strong><span>fan out</span></div>
          <i>→</i>
          <div className="flowModels">
            <article><small>TEXT</small><strong>QWEN3</strong><span>instruction-aware</span></article>
            <article><small>MULTIMODAL</small><strong>SIGLIP2</strong><span>paired towers</span></article>
            <article><small>GENERAL</small><strong>NOMIC</strong><span>text + vision</span></article>
          </div>
          <i>→</i>
          <div className="flowSource hot"><small>POST SEARCH</small><strong>Z-SCORE</strong><span>comparable ranks</span></div>
        </div>
        <div className="embeddingPrinciples">
          <article><b>01</b><h3>The store stays unchanged</h3><p>Keep vectors in collections that match their model, checkpoint and dimension. Multi-model behavior belongs entirely to embedding and application orchestration.</p></article>
          <article><b>02</b><h3>Never mix raw vectors</h3><p>A Qwen3 vector cannot query a SigLIP2 index meaningfully. The model name is part of the collection contract, not optional metadata.</p></article>
          <article><b>03</b><h3>Merge scores, not vectors</h3><p>Search every space first. Standardize each candidate group with the existing z-score calibrator, then rank on the calibrated score.</p></article>
        </div>
      </section>

      <section className="detailBand">
        <div className="wrap splitDetail">
          <div><div className="eyebrow"><span /> NAMED MODEL REGISTRY</div><h2>Configure the contract, not just a file.</h2><p>Each entry owns its tokenizer, ONNX graph, preprocessing profile, queue and execution settings. Model files remain deployment assets and must come from matching checkpoint revisions.</p><ul className="checkList"><li>Qwen3: left padding, last-token pooling and optional Matryoshka output</li><li>SigLIP2: paired text/vision checkpoint and fixed-resolution vision export</li><li>Nomic: mean pooling, layer normalization and shared text/image use cases</li></ul></div>
          <CodeBlock label="APPSETTINGS.JSON">{config}</CodeBlock>
        </div>
      </section>

      <section className="detailSection wrap">
        <div className="detailHeading"><span>02</span><div><h2>Fan out only where you search.</h2><p>The REST multi endpoint embeds one text across named models in one call. The .NET client exposes model selection on gRPC single and batch calls, so parallel fan-out stays explicit and bounded.</p></div></div>
        <div className="clientCodeGrid codeGrid"><CodeBlock label="C# · EMBED + SEARCH">{query}</CodeBlock><CodeBlock label="C# · CALIBRATE + LINQ">{merge}</CodeBlock></div>
      </section>

      <section className="detailBand">
        <div className="wrap">
          <div className="detailHeading"><span>03</span><div><h2>Post-search reconciliation.</h2><p>Raw cosine values from different models live on different scales. Per-group z-score calibration preserves the ordering inside each result set and creates a common ranking signal across them.</p></div></div>
          <div className="scoreRail">
            <article><small>QWEN3 RAW</small><strong>0.79</strong><i>local rank 1</i></article>
            <span>normalize per group →</span>
            <article><small>CALIBRATED</small><strong>+1.82σ</strong><i>global candidate</i></article>
            <article><small>SIGLIP2 RAW</small><strong>0.31</strong><i>local rank 1</i></article>
            <span>normalize per group →</span>
            <article><small>CALIBRATED</small><strong>+1.47σ</strong><i>global candidate</i></article>
          </div>
          <div className="benchmarkCaveat"><b>APPLICATION POLICY STAYS IN LINQ</b><p>No additional reconciliation helper is necessary. After calibration, group by a stable metadata ID or use <code>DistinctBy</code>; keep the highest <code>CalibratedScore</code> for each logical document.</p></div>
        </div>
      </section>

      <section className="detailSection wrap">
        <div className="detailHeading"><span>04</span><div><h2>Production guardrails.</h2><p>Model identity is an index invariant. Treat changes to weights, tokenizer, pooling, normalization or output dimension as a migration to a new collection.</p></div></div>
        <div className="useGrid"><article><span>PIN</span><h3>Version the pair</h3><p>Pin tokenizer and checkpoint revisions and deploy them atomically.</p></article><article><span>LABEL</span><h3>Record the space</h3><p>Store model, dimension and source revision in collection metadata.</p></article><article><span>DEPTH</span><h3>Fetch candidates</h3><p>Retrieve more than the final top-k so z-score has a useful distribution.</p></article><article><span>MEASURE</span><h3>Evaluate the merge</h3><p>Measure per-space recall separately from the calibrated global ranking.</p></article></div>
        <div className="embeddingDocsCta"><div><small>DEVELOPER GUIDE</small><strong>Configuration, REST, gRPC, LINQ and migration examples.</strong></div><a href="https://github.com/ppossanzini/Jigen/blob/main/docs/embeddings/multi-model.md">Read the exhaustive guide ↗</a></div>
      </section>
    </DetailShell>
  );
}
