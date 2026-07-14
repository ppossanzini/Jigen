using Hikyaku;
using Jigen.Filtering;

namespace Jigen.Core.Query.collections;

public class SearchVector : IRequest<IEnumerable<SearchVectorResultItem>>
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public float[] Embeddings { get; set; }
  public int Top { get; set; }
  public IFilterExpression Filter { get; set; }

  /// <summary>Per-query HNSW beam width; 0 = index default. Ignored by exact indexes.</summary>
  public int EfSearch { get; set; }

  /// <summary>Return keys and scores only, without the content bytes.</summary>
  public bool NoContent { get; set; }

  /// <summary>Drop results scoring below this similarity.</summary>
  public float? MinScore { get; set; }
}

public class SearchVectorResultItem
{
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float Score { get; set; }
}
