namespace Jigen.Core.Command.collections;

public class SetVector
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public object Content { get; set; }
  public float[] Embeddings { get; set; }
}