#!/usr/bin/env python3
"""Qdrant multi-vector probe: ingest / index / search stats on K vectors per point.

Mirrors the Jigen mv:K macro-benchmark so the two are comparable: same shape
(10k documents, 128 dimensions, K=4), same query count (500), same top (10).
Writes nothing outside /tmp/qdrant-mv.
"""

import json
import random
import statistics
import time
import urllib.request

BASE = "http://localhost:6333"
COLLECTION = "mvbench"
DOCS = 10000
DIM = 128
K = 4
QUERIES = 500
TOP = 10
BATCH = 500


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(
        BASE + path, data=data, method=method,
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=300) as response:
        return json.loads(response.read().decode())


def unit_vector(rng):
    vector = [rng.uniform(-1, 1) for _ in range(DIM)]
    norm = sum(v * v for v in vector) ** 0.5
    return [v / norm for v in vector]


def percentile(values, p):
    ordered = sorted(values)
    index = min(len(ordered) - 1, int(round(p / 100 * (len(ordered) - 1))))
    return ordered[index]


rng = random.Random(42)
docs = [[unit_vector(rng) for _ in range(K)] for _ in range(DOCS)]
query_vector = unit_vector(rng)

call("DELETE", f"/collections/{COLLECTION}")
call("PUT", f"/collections/{COLLECTION}", {
    "vectors": {
        "mv": {
            "size": DIM,
            "distance": "Cosine",
            "multivector_config": {"comparator": "max_sim"},
        }
    },
    "optimizers_config": {"indexing_threshold": 1000},
})

started = time.perf_counter()
for offset in range(0, DOCS, BATCH):
    points = [
        {"id": offset + i + 1, "vector": {"mv": docs[offset + i]}}
        for i in range(min(BATCH, DOCS - offset))
    ]
    call("PUT", f"/collections/{COLLECTION}/points?wait=true", {"points": points})
ingest_seconds = time.perf_counter() - started

print(f"ingest                {DOCS / ingest_seconds:10.0f} docs/s "
      f"({DOCS * K / ingest_seconds:.0f} vectors/s, {ingest_seconds:.1f}s)")

# Wait for the HNSW index to cover every point.
index_started = time.perf_counter()
while True:
    info = call("GET", f"/collections/{COLLECTION}")["result"]
    if info["indexed_vectors_count"] >= info["points_count"]:
        break
    if time.perf_counter() - index_started > 600:
        print("index wait timed out")
        break
    time.sleep(1)
print(f"index                 {time.perf_counter() - index_started:10.2f} s")

for _ in range(20):
    call("POST", f"/collections/{COLLECTION}/points/query",
         {"query": [query_vector], "limit": TOP, "using": "mv"})

latencies = []
for _ in range(QUERIES):
    began = time.perf_counter()
    call("POST", f"/collections/{COLLECTION}/points/query",
         {"query": [query_vector], "limit": TOP, "using": "mv"})
    latencies.append((time.perf_counter() - began) * 1000)

print(f"search p50            {statistics.median(latencies):10.2f} ms")
print(f"search p95            {percentile(latencies, 95):10.2f} ms")

info = call("GET", f"/collections/{COLLECTION}")["result"]
result = call("POST", f"/collections/{COLLECTION}/points/query",
              {"query": [query_vector], "limit": TOP, "using": "mv"})["result"]
print(f"points_count          {info['points_count']:10d}")
print(f"indexed_vectors_count {info['indexed_vectors_count']:10d}  "
      f"(stored vectors: {DOCS * K})")
print(f"segments_count        {info['segments_count']:10d}")
unique_ids = len(set(p["id"] for p in result["points"]))
print(f"returned results      {len(result['points']):10d}  (unique point ids: {unique_ids})")
