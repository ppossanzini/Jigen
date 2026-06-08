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
}

public class SearchVectorResultItem
{
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float Score { get; set; }
}
