import type { Metadata } from "next";
import { CodeBlock, DetailShell } from "../_components/DetailShell";

export const metadata: Metadata = { title: "Architecture — Jigen DB", description: "How Jigen moves vectors from append-only storage to exact or HNSW search." };

const files = `database/
├── collection.data      # serialized content
├── collection.vectors   # contiguous float vectors
├── collection.keys      # key → position lookup
└── hnsw/
    ├── graph.dat         # memory-mapped graph
    └── vectors.dat       # index vector storage`;

export default function ArchitecturePage() {
  return <DetailShell eyebrow="SYSTEM ARCHITECTURE" index="01 / 04" title="A short path from write to nearest neighbor." intro="Jigen keeps storage, indexing and query execution explicit. The embedded engine is the foundation; the server adds an operational boundary without changing the underlying data model.">
    <section className="detailSection wrap">
      <div className="detailHeading"><span>01</span><div><h2>The data path</h2><p>An append reaches durable storage first. Search then chooses the exact or approximate path according to the configured index.</p></div></div>
      <div className="archFlow"><article><b>01</b><strong>Typed content</strong><p>MessagePack payload plus stable vector key.</p></article><i>→</i><article><b>02</b><strong>Append-only store</strong><p>Sequential writes to memory-mapped files.</p></article><i>→</i><article><b>03</b><strong>Indexer</strong><p>Exact scan, lazy switch or persistent HNSW.</p></article><i>→</i><article><b>04</b><strong>Top-k results</strong><p>Scores, content and optional metadata filters.</p></article></div>
    </section>
    <section className="detailBand"><div className="wrap splitDetail"><div><div className="eyebrow"><span /> STORAGE MODEL</div><h2>Disk is part of the design.</h2><p>Vectors and graph data stay in memory-mapped files rather than being copied into the managed heap. The operating system controls page residency; Jigen controls the layout and lifecycle.</p><ul className="checkList"><li>Append-only writes reduce random I/O</li><li>Periodic checkpoints define durability boundaries</li><li>Reopen reconstructs state from persisted files</li><li>Crash recovery handles interrupted writes</li></ul></div><CodeBlock label="ON-DISK LAYOUT">{files}</CodeBlock></div></section>
    <section className="detailSection wrap"><div className="detailHeading"><span>02</span><div><h2>Search strategy is a workload decision.</h2><p>There is no universal index. Jigen exposes exact, HNSW and lazy indexing as different cost profiles.</p></div></div><div className="compareGrid"><article><small>EXACT</small><h3>Brute force</h3><p>Scans every vector. Ideal for small collections, validation and workloads where perfect recall matters more than linear cost.</p><dl><div><dt>Recall</dt><dd>1.000</dd></div><div><dt>Build cost</dt><dd>None</dd></div></dl></article><article className="active"><small>APPROXIMATE</small><h3>HNSW</h3><p>Traverses a multi-layer proximity graph. Tune M, EfConstruction and EfSearch to balance memory, build time and recall.</p><dl><div><dt>Complexity</dt><dd>Sub-linear</dd></div><div><dt>Persistence</dt><dd>Disk-backed</dd></div></dl></article><article><small>ADAPTIVE</small><h3>Lazy indexer</h3><p>Starts exact, then reconciles into HNSW after a threshold. Fast bulk ingest first, approximate search when scale requires it.</p><dl><div><dt>Ingest</dt><dd>Deferred</dd></div><div><dt>Switch</dt><dd>Threshold</dd></div></dl></article></div></section>
  </DetailShell>;
}
