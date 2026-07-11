# Execution Providers

The embedding model runs on ONNX Runtime, which supports multiple hardware execution providers (EPs). Jigen selects the provider through `EmbeddingGeneratorOptions.ExecutionProvider` (see [configuration](configuration.md)), and the native ONNX Runtime package linked into the binary must match — selected at build time with the `JigenOnnxRuntimeFlavor` MSBuild property on `Jigen.SemanticTools.csproj`.

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

## Fallback behavior

Execution provider registration is defensive:

- An unrecognized `ExecutionProvider` value logs a warning and silently uses CPU.
- If registering the requested provider throws (e.g. the matching native runtime package was not linked in, or the hardware/driver is unavailable), Jigen logs a warning and falls back to CPU rather than failing to start.

This means requesting `cuda`, `dml`, `rocm`, `migraphx`, or `openvino` on a binary built with the `Cpu` flavor (or on a machine without the corresponding driver) does not crash the process — it just runs on CPU, typically much slower, so registration failures are worth monitoring in logs when a non-CPU provider is expected.

## Choosing a model and batch size

- `MaxBatchSize`: keep at `1` on CPU (the default); intra-op parallelism already saturates CPU cores, and batching mixed-length inputs pads shorter sequences to the longest one in the batch, wasting compute. Raise it to somewhere in the 8–32 range on GPU providers, where fusing sequences into one inference call amortizes kernel launch and data-transfer overhead.
- Model precision: prefer the `int8` quantized model on CPU (faster, no measurable ranking degradation in testing) and an `fp16` model on GPU.
- `GpuDeviceId` selects the target device index for `cuda`, `dml`, `rocm`, and `migraphx`.

See [configuration](configuration.md) for the full `EmbeddingGeneratorOptions` reference and a production configuration example.
