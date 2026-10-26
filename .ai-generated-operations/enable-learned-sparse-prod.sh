#!/usr/bin/env bash
# Enables BGE-M3 learned sparse weights on the production cluster (namespace jigendb).
# Run the steps one at a time; each one is safe to repeat.
set -euo pipefail

NS=jigendb
HEAD_URL=https://huggingface.co/BAAI/bge-m3/resolve/main/sparse_linear.pt
LOCAL=/tmp/sparse_linear.pt

step1_download() {
  # From a machine with internet access (the worker image has no curl/wget).
  curl -fL -o "$LOCAL" "$HEAD_URL"
  ls -l "$LOCAL"   # expected: a few KB (1024 weights + 1 bias)
}

step2_copy_to_volume() {
  # onnx-pv-claim is RWX and shared by every replica: one copy is enough.
  local pod
  pod=$(kubectl -n "$NS" get pod -l app=jigendb-embeddings -o jsonpath='{.items[0].metadata.name}')
  kubectl -n "$NS" cp "$LOCAL" "$pod:/data/onnx/bge-m3/sparse_linear.pt"
  for p in $(kubectl -n "$NS" get pod -l app=jigendb-embeddings -o name); do
    kubectl -n "$NS" exec "${p#pod/}" -- ls -l /data/onnx/bge-m3/sparse_linear.pt
  done
}

step3_patch_config() {
  # Adds GeneratorOptions.Sparse.HeadPath to the worker configuration.
  # Harmless on the current image (unknown keys are ignored) until step 4.
  kubectl -n "$NS" get cm jigen-embeddings-config -o jsonpath='{.data.appsettings\.Production\.json}' > /tmp/emb.json
  cp /tmp/emb.json /tmp/emb.json.bak
  python3 - <<'EOF'
import json
path = "/tmp/emb.json"
cfg = json.load(open(path))
cfg["JigenEmbeddings"]["GeneratorOptions"]["Sparse"] = {"HeadPath": "sparse_linear.pt"}
json.dump(cfg, open(path, "w"), indent=2)
EOF
  kubectl -n "$NS" create cm jigen-embeddings-config \
    --from-file=appsettings.Production.json=/tmp/emb.json --dry-run=client -o yaml | kubectl -n "$NS" apply -f -
}

step4_rollout() {
  # Only after the new images (with Sparse.HeadPath support) are published.
  kubectl -n "$NS" rollout restart deploy/jigendb-embeddings
  kubectl -n "$NS" rollout status deploy/jigendb-embeddings --timeout=15m
  kubectl -n "$NS" rollout restart sts/jigendb
  kubectl -n "$NS" rollout status sts/jigendb --timeout=15m
}

step5_verify() {
  for p in $(kubectl -n "$NS" get pod -l app=jigendb-embeddings -o name); do
    kubectl -n "$NS" logs "${p#pod/}" | grep -m1 "Loaded learned sparse head" || echo "${p#pod/}: head NOT loaded"
  done
}

rollback_config() {
  kubectl -n "$NS" create cm jigen-embeddings-config \
    --from-file=appsettings.Production.json=/tmp/emb.json.bak --dry-run=client -o yaml | kubectl -n "$NS" apply -f -
  kubectl -n "$NS" rollout restart deploy/jigendb-embeddings
}

"${1:?usage: $0 step1_download|step2_copy_to_volume|step3_patch_config|step4_rollout|step5_verify|rollback_config}"
