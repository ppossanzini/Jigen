using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace Jigen.SemanticTools;

/// <summary>
/// Shared ONNX Runtime execution-provider registration for the semantic
/// engines (text and image embeddings). Registration is defensive: an unknown
/// provider logs a warning and uses CPU; a provider that fails to register
/// (missing native package or unavailable hardware/driver) also falls back to
/// CPU rather than failing to start.
/// </summary>
internal static class OnnxExecutionProvider
{
  public static void Append(SessionOptions sessionOptions, string executionProvider, int gpuDeviceId, ILogger logger)
  {
    var provider = executionProvider?.Trim();
    if (string.IsNullOrEmpty(provider) || provider.Equals("cpu", StringComparison.OrdinalIgnoreCase))
      return;

    try
    {
      if (provider.Equals("cuda", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_CUDA(gpuDeviceId);
      }
      else if (provider.Equals("dml", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_DML(gpuDeviceId);
      }
      else if (provider.StartsWith("openvino", StringComparison.OrdinalIgnoreCase))
      {
        var separatorIndex = provider.IndexOf(':');
        var device = separatorIndex >= 0 ? provider[(separatorIndex + 1)..] : "GPU";
        sessionOptions.AppendExecutionProvider_OpenVINO(device);
      }
      else if (provider.Equals("coreml", StringComparison.OrdinalIgnoreCase))
      {
        // MLProgram targets the modern CoreML format; ALL lets CoreML pick
        // between ANE, GPU and CPU per operator.
        sessionOptions.AppendExecutionProvider("CoreML", new Dictionary<string, string>
        {
          ["ModelFormat"] = "MLProgram",
          ["MLComputeUnits"] = "ALL"
        });
      }
      else if (provider.Equals("rocm", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_ROCm(gpuDeviceId);
      }
      else if (provider.Equals("migraphx", StringComparison.OrdinalIgnoreCase))
      {
        sessionOptions.AppendExecutionProvider_MIGraphX(gpuDeviceId);
      }
      else
      {
        logger?.LogWarning("Unknown execution provider '{Provider}'. Using CPU.", provider);
        return;
      }

      logger?.LogInformation("Registered execution provider {Provider} for the model.", provider);
    }
    catch (Exception ex)
    {
      logger?.LogWarning(
        ex,
        "Failed to register execution provider '{Provider}'. Falling back to CPU. " +
        "Ensure the process was built with the matching ONNX Runtime native package (JigenOnnxRuntimeFlavor).",
        provider);
    }
  }
}
