#!/usr/bin/env python3
"""Generates a tiny synthetic ONNX "vision embedding" model used as a test fixture.

The model mimics the I/O contract of nomic-embed-vision-v1.5 as consumed by
OnnxImageEmbeddingGenerator:
  - input:  pixel_values        [batch, 3, 224, 224] float (NCHW, normalized)
  - output: last_hidden_state   [batch, 197, 3] float
    where the CLS token (index 0) equals the per-channel mean of the input
    over H,W — so tests can assert that preprocessing (resize + normalize with
    the configured mean/std) produced exactly the values the model received.

Graph (opset 13):
  pixel_values --ReduceMean(axes=[2,3])--> [B,3,1,1] --Reshape--> [B,1,3]
               --Pad(0,196,0 on seq dim)--> [B,197,3] = last_hidden_state

Output: {output_dir}/tiny_vision_model.onnx
"""

import sys
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper


def build_model() -> onnx.ModelProto:
    # Batch dim is symbolic so the same fixture works for batched inference.
    pixel_values = helper.make_tensor_value_info(
        "pixel_values", TensorProto.FLOAT, [None, 3, 224, 224]
    )
    last_hidden_state = helper.make_tensor_value_info(
        "last_hidden_state", TensorProto.FLOAT, [None, 197, 3]
    )

    # ReduceMean over H,W (attribute form, opset 13)
    reduce_mean = helper.make_node(
        "ReduceMean",
        ["pixel_values"],
        ["means"],
        name="reduce_mean",
        axes=[2, 3],
        keepdims=1,
    )

    # [B,3,1,1] -> [B,1,3]  (batch, seq=1, hidden=3)
    shape = helper.make_tensor("shape", TensorProto.INT64, [3], [0, 1, 3])
    reshape = helper.make_node("Reshape", ["means", "shape"], ["cls"], name="reshape")

    # [B,1,3] -> [B,197,3]: pad the sequence dim with 196 zero tokens
    pads = helper.make_tensor("pads", TensorProto.INT64, [6], [0, 0, 0, 0, 196, 0])
    pad = helper.make_node(
        "Pad", ["cls", "pads"], ["last_hidden_state"], name="pad", mode="constant"
    )

    graph = helper.make_graph(
        [reduce_mean, reshape, pad],
        "tiny_vision",
        [pixel_values],
        [last_hidden_state],
        initializer=[shape, pads],
    )
    model = helper.make_model(
        graph,
        opset_imports=[helper.make_opsetid("", 13)],
        producer_name="jigen-test-fixtures",
    )
    model.ir_version = 8
    onnx.checker.check_model(model)
    return model


def main() -> None:
    output_dir = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / "tiny_vision_model.onnx"
    model = build_model()

    # Sanity check: run a forward pass by hand on a solid-color input.
    r, g, b = 100, 150, 200
    mean = [0.48145466, 0.4578275, 0.40821073]
    std = [0.26862954, 0.26130258, 0.27577711]
    expected_cls = np.array(
        [((r / 255.0) - mean[0]) / std[0],
         ((g / 255.0) - mean[1]) / std[1],
         ((b / 255.0) - mean[2]) / std[2]],
        dtype=np.float32,
    )
    print("expected CLS (pre-L2):", expected_cls)

    onnx.save(model, output)
    print(f"written: {output} ({output.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
