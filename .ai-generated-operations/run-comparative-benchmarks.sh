#!/usr/bin/env bash
# run-comparative-benchmarks.sh
# Orchestrates the full comparative benchmark pipeline:
#   1. Start Podman containers (Qdrant, Milvus, pgvector)
#   2. Generate/load shared datasets
#   3. Run macro-benchmarks (ingestion, lifecycle)
#   4. Run micro-benchmarks (BenchmarkDotNet search)
#   5. Generate summary report
#
# Usage:
#   ./run-comparative-benchmarks.sh              # Full pipeline, all DBs, random-10k
#   ./run-comparative-benchmarks.sh --dataset random-100k,sift-128-euclidean
#   ./run-comparative-benchmarks.sh --dbs jigendb,qdrant --dataset random-10k
#   ./run-comparative-benchmarks.sh --macro-only --dbs ALL --dataset random-10k,random-100k
#   ./run-comparative-benchmarks.sh --micro-only --dbs ALL --dataset random-100k

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BENCHMARK_DIR="$PROJECT_DIR/tests/JigenBenchmarks/ComparativeBenchmarks"
RESULTS_DIR="$BENCHMARK_DIR/Results"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# ── Defaults ──
DBS="ALL"
DATASET="random-10k"
RUN_MACRO=true
RUN_MICRO=true
GENERATE=true

# Detect compose command: prefer podman-compose, fall back to docker compose
if command -v podman-compose &>/dev/null; then
    COMPOSE_CMD="podman-compose"
elif command -v docker &>/dev/null && docker compose version &>/dev/null 2>&1; then
    COMPOSE_CMD="docker compose"
else
    echo "ERROR: Neither podman-compose nor docker compose found." >&2
    exit 1
fi

# ── Parse args ──
while [[ $# -gt 0 ]]; do
    case "$1" in
        --dbs)       DBS="$2"; shift 2 ;;
        --dataset)   DATASET="$2"; shift 2 ;;
        --macro-only) RUN_MICRO=false; shift ;;
        --micro-only) RUN_MACRO=false; shift ;;
        --no-generate) GENERATE=false; shift ;;
        --help)
            echo "Usage: $0 [options]"
            echo "  --dbs DB1,DB2,...     DBs to test (jigendb-hnsw,jigendb-brute,qdrant,milvus,pgvector,ALL) [default: ALL]"
            echo "  --dataset NAME,...    Datasets to use [default: random-10k]"
            echo "  --macro-only          Only run macro-benchmarks"
            echo "  --micro-only          Only run micro-benchmarks"
            echo "  --no-generate         Skip dataset generation"
            exit 0
            ;;
        *) echo "Unknown arg: $1"; exit 1 ;;
    esac
done

# ── Colors ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[$(date +%H:%M:%S)]${NC} $*"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
err()  { echo -e "${RED}[ERROR]${NC} $*"; }

# ── Step 0: Ensure shared data directory exists ──
mkdir -p /tmp/benchmark-data

# ── Step 1: Start Podman services ──
log "=== Step 1: Starting Podman services ==="
cd "$PROJECT_DIR"
$COMPOSE_CMD up -d qdrant milvus pgvector 2>&1 || {
    err "Compose failed. Is podman-compose installed?"
    exit 1
}

# Wait for services to be healthy
log "Waiting for services to be ready..."
for i in {1..30}; do
    QDRANT_OK=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:6333/health 2>/dev/null || echo "000")
    MILVUS_OK=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:9091/healthz 2>/dev/null || echo "000")
    PGVECTOR_OK=$(podman exec pgvector pg_isready -U benchmark 2>/dev/null || echo "no")

    if [[ "$QDRANT_OK" == "200" && "$MILVUS_OK" == "200" && "$PGVECTOR_OK" == *"accepting connections"* ]]; then
        log "All services ready!"
        break
    fi

    if [[ $i -eq 30 ]]; then
        err "Services not ready after 30s. Check: $COMPOSE_CMD logs"
        exit 1
    fi
    sleep 1
done

# ── Step 2: Generate datasets ──
if $GENERATE; then
    log "=== Step 2: Generating datasets ==="
    dotnet run -c Release --project "$BENCHMARK_DIR" -- generate
fi

# ── Step 3: Create results directory ──
mkdir -p "$RESULTS_DIR"
RUN_DIR="$RESULTS_DIR/$TIMESTAMP"
mkdir -p "$RUN_DIR"

# ── Step 4: Macro benchmarks ──
if $RUN_MACRO; then
    log "=== Step 3: Running macro-benchmarks ==="
    dotnet run -c Release --project "$BENCHMARK_DIR" -- macro "$DBS" "$DATASET" \
        2>&1 | tee "$RUN_DIR/macro-results.md"

    log "Macro results saved to $RUN_DIR/macro-results.md"
fi

# ── Step 5: Micro benchmarks (BenchmarkDotNet) ──
if $RUN_MICRO; then
    log "=== Step 4: Running micro-benchmarks ==="
    dotnet run -c Release --project "$BENCHMARK_DIR" -- micro "$DBS" "$DATASET" \
        2>&1 | tee "$RUN_DIR/micro-results.md"

    log "Micro results saved to $RUN_DIR/micro-results.md"
fi

# ── Step 6: Summary ──
log "=== Done! ==="
log "Results: $RUN_DIR/"
log ""
log "Quick view:"
if [[ -f "$RUN_DIR/macro-results.md" ]]; then
    echo ""
    echo "── Macro Benchmarks ──"
    grep -E '^\|' "$RUN_DIR/macro-results.md" | head -15
fi

echo ""
log "To stop services:  $COMPOSE_CMD down"
log "To view all data:  ls -la /tmp/benchmark-data/"
