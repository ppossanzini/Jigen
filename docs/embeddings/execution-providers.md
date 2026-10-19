# Execution Providers

The embedding models run on ONNX Runtime, which supports multiple hardware execution providers (EPs). Both the text engine (`OnnxEmbeddingGenerator`) and the image engine (`OnnxImageEmbeddingGenerator`) select the provider through their respective options (`EmbeddingGeneratorOptions.ExecutionProvider` / `ImageEmbeddingGeneratorOptions.ExecutionProvider`, see [configuration](configuration.md)), via a **shared registration helper** (`OnnxExecutionProvider`) — so the fallback behavior and build requirements below apply identically to both. The native ONNX Runtime package linked into the binary must match — selected at build time with the `JigenOnnxRuntimeFlavor` MSBuild property on `Jigen.SemanticTools.csproj`.

## Supported providers

| `ExecutionProvider` value | Hardware / platform | Build flavor (`JigenOnnxRuntimeFlavor`) | Notes |
|---|---|---|---|
| `cpu` | Any | `Cpu` (default) | No native GPU package required; used when `ExecutionProvider` is empty or `"cpu"`. |
| `cuda` | NVIDIA GPU, Linux/Windows | `Gpu` (`-p:JigenOnnxRuntimeFlavor=Gpu`, package `Microsoft.ML.OnnxRuntime.Gpu`) | Registered via `AppendExecutionProvider_CUDA(GpuDeviceId)`. Implemented, pending validation on target NVIDIA hardware. |
| `dml` | Windows GPU (DirectML) | `DirectML` (package `Microsoft.ML.OnnxRuntime.DirectML`) | Registered via `AppendExecutionProvider_DML(GpuDeviceId)`. |
| `openvino` / `openvino:DEVICE` | Intel CPU/GPU/NPU, Windows x64 only | `OpenVino` (package `Intel.ML.OnnxRuntime.OpenVino`, NuGet-only) | Device defaults to `GPU` when omitted, e.g. `openvino:GPU`, `openvino:CPU`, `openvino:NPU`. |
| `coreml` | Apple Silicon (macOS) | Included in the default (`Cpu`) package | Registered with `ModelFormat=MLProgram`, `MLComputeUnits=ALL`, letting CoreML choose between ANE, GPU and CPU per operator. |
| `rocm` | AMD GPU | None — requires a custom ONNX Runtime native build; no NuGet package | Registered via `AppendExecutionProvider_ROCm(GpuDeviceId)`. Implemented, pending validation on target AMD hardware. |
| `migraphx` | AMD GPU (MIGraphX) | None — requires a custom ONNX Runtime native build; no NuGet package | Registered via `AppendExecutionProvider_MIGraphX(GpuDeviceId)`. |

Vulkan is not available as an ONNX Runtime execution provider and is not planned.

## Runtime images

The release publishes one embedding worker image for each packaged ONNX Runtime flavor:

| Image | Flavor | Runtime platform |
|---|---|---|
| `ppossanzini/jigen-embeddings` | `Cpu` | Linux |
| `ppossanzini/jigen-embeddings-gpu` | `Gpu` | Linux with NVIDIA CUDA |
| `ppossanzini/jigen-embeddings-directml` | `DirectML` | Package artifact only; requires a Windows container base before deployment |
| `ppossanzini/jigen-embeddings-openvino` | `OpenVino` | Package artifact only; requires a Windows x64 container base before deployment |

`Microsoft.ML.OnnxRuntime.Gpu` 1.27.0 requires CUDA 13.0 and cuDNN 9 by default. The GPU image uses `nvidia/cuda:13.0.2-cudnn-runtime-ubuntu24.04` and ASP.NET Runtime 10. The host requires an NVIDIA driver compatible with CUDA 13. The cluster must expose the GPU through NVIDIA Container Toolkit and the NVIDIA device plugin or GPU Operator.

Publish the GPU flavor with:

```bash
dotnet publish src/Embedding/Jigen.Embedding/Jigen.Embedding.csproj \
	-p:JigenOnnxRuntimeFlavor=Gpu \
	-o publish/embeddings-gpu
```

## Fallback behavior

Execution provider registration is defensive:

- An unrecognized `ExecutionProvider` value logs a warning and silently uses CPU.
- If registering the requested provider throws (e.g. the matching native runtime package was not linked in, or the hardware/driver is unavailable), Jigen logs a warning and falls back to CPU rather than failing to start.

This means requesting `cuda`, `dml`, `rocm`, `migraphx`, or `openvino` on a binary built with the `Cpu` flavor (or on a machine without the corresponding driver) does not crash the process — it just runs on CPU, typically much slower, so registration failures are worth monitoring in logs when a non-CPU provider is expected.

GPU deployments must contain the `Registered execution provider cuda` log entry. A registration warning means that the worker is running on CPU.

## Choosing a model and batch size

- `MaxBatchSize`: keep at `1` on CPU (the default); intra-op parallelism already saturates CPU cores, and batching mixed-length inputs pads shorter sequences to the longest one in the batch, wasting compute. Raise it to somewhere in the 8–32 range on GPU providers, where fusing sequences into one inference call amortizes kernel launch and data-transfer overhead. For images the padding concern does not apply (fixed-size input), so batching is more useful than for text even on CPU.
- Model precision: prefer the `int8` quantized model on CPU (faster, no measurable ranking degradation in testing) and an `fp16` model on GPU. The same guidance applies to the vision model (`nomic-embed-vision-v1.5`).
- `GpuDeviceId` selects the target device index for `cuda`, `dml`, `rocm`, and `migraphx`.

See [configuration](configuration.md) for the full `EmbeddingGeneratorOptions` and `ImageEmbeddingGeneratorOptions` reference and a production configuration example.
