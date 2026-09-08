using Jigen.SemanticTools;

namespace Jigen.Embedding.Handlers;

public interface IEmbeddingGeneratorRegistry
{
  string DefaultModel { get; }
  IReadOnlyCollection<string> ModelNames { get; }
  IEmbeddingGenerator Get(string model = null);
  string GetDefaultTask(string model = null);
  string PrepareInput(string model, string task, string input);
}

internal sealed class EmbeddingGeneratorRegistry(
  string defaultModel,
  IReadOnlyDictionary<string, IEmbeddingGenerator> generators,
  IReadOnlyDictionary<string, string> defaultTasks,
  IReadOnlyDictionary<string, EmbeddingModelProfile> profiles) : IEmbeddingGeneratorRegistry, IDisposable
{
  public string DefaultModel { get; } = defaultModel;
  public IReadOnlyCollection<string> ModelNames { get; } = generators.Keys.ToArray();

  public IEmbeddingGenerator Get(string model = null)
  {
    var name = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
    if (generators.TryGetValue(name, out var generator))
      return generator;

    throw new KeyNotFoundException(
      $"Embedding model '{name}' is not configured. Available models: {string.Join(", ", ModelNames)}.");
  }

  public string GetDefaultTask(string model = null)
  {
    var name = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
    _ = Get(name);
    return defaultTasks.TryGetValue(name, out var task) ? task : null;
  }

  public string PrepareInput(string model, string task, string input)
  {
    var name = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
    _ = Get(name);
    if (string.IsNullOrWhiteSpace(task))
      return input;

    return profiles[name] switch
    {
      EmbeddingModelProfile.Qwen3 => $"Instruct: {task}\nQuery:{input}",
      EmbeddingModelProfile.SigLip2 => input,
      _ => $"{task}: {input}"
    };
  }

  public void Dispose()
  {
    foreach (var generator in generators.Values.Distinct())
      if (generator is IDisposable disposable)
        disposable.Dispose();
  }
}

internal sealed class DefaultEmbeddingGenerator(IEmbeddingGeneratorRegistry registry) : IEmbeddingGenerator
{
  private IEmbeddingGenerator Inner => registry.Get();

  public float[] GenerateEmbedding(string input) => Inner.GenerateEmbedding(input);
  public float[] GenerateEmbedding(string task, string input) => Inner.GenerateEmbedding(task, input);
  public float[][] GenerateEmbeddings(IReadOnlyList<string> inputs) => Inner.GenerateEmbeddings(inputs);
  public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default) =>
    Inner.GenerateEmbeddingAsync(input, cancellationToken);
  public Task<float[]> GenerateEmbeddingAsync(string task, string input, CancellationToken cancellationToken = default) =>
    Inner.GenerateEmbeddingAsync(task, input, cancellationToken);
  public Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
    Inner.GenerateEmbeddingsAsync(inputs, cancellationToken);
}
