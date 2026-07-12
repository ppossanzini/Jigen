namespace Jigen;

public class VectorCollectionOptions<T>
  where T : class, new()
{
  public int Dimensions = 1536;
  public string Name = typeof(T).Namespace + "." + typeof(T).Name;

  public IDocumentSerializer DocumentSerializer { get; set; } = MessagePackDocumentSerializer.Instance;

  /// <summary>
  /// Turns a sentence into an embedding for the sentence-based Add/Search
  /// overloads (e.g. <c>generator.GenerateEmbedding</c> from
  /// Jigen.SemanticTools). When unset, those overloads throw.
  /// </summary>
  public Func<string, float[]> SentenceEmbedder { get; set; }
}