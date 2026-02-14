using Hikyaku;

namespace Jigen.Core.Command.collections;

public class SetRawVector : IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  public float[] Embeddings { get; set; }
}