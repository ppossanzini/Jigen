namespace JigenTests;

/// <summary>
/// Serializes the tests that load an ONNX model. The runtime registers the
/// <c>ai.onnx.contrib</c> domain once per process and rejects a second
/// registration, so two of these tests initializing together would make one
/// fail for a reason unrelated to what it checks.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OnnxModelCollection
{
    public const string Name = "onnx-models";
}
