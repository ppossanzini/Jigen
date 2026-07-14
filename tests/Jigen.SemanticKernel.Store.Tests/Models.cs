using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Store.Tests;

public sealed class Article
{
  [VectorStoreKey]
  public Guid Id { get; set; }

  [VectorStoreData]
  public string Title { get; set; } = string.Empty;

  [VectorStoreData]
  public string Category { get; set; } = string.Empty;

  [VectorStoreVector(8)]
  public ReadOnlyMemory<float> Embedding { get; set; }
}

public sealed class ArticleWithStringKey
{
  [VectorStoreKey]
  public string Slug { get; set; } = string.Empty;

  [VectorStoreData]
  public string Title { get; set; } = string.Empty;

  [VectorStoreVector(8)]
  public float[] Embedding { get; set; } = [];
}

public sealed class NoVectorRecord
{
  [VectorStoreKey]
  public Guid Id { get; set; }

  [VectorStoreData]
  public string Title { get; set; } = string.Empty;
}

public sealed class TwoVectorsRecord
{
  [VectorStoreKey]
  public Guid Id { get; set; }

  [VectorStoreVector(8)]
  public float[] EmbeddingA { get; set; } = [];

  [VectorStoreVector(8)]
  public float[] EmbeddingB { get; set; } = [];
}

public sealed class NoKeyRecord
{
  [VectorStoreData]
  public string Title { get; set; } = string.Empty;

  [VectorStoreVector(8)]
  public float[] Embedding { get; set; } = [];
}

public sealed class IntKeyRecord
{
  [VectorStoreKey]
  public int Id { get; set; }

  [VectorStoreVector(8)]
  public float[] Embedding { get; set; } = [];
}
