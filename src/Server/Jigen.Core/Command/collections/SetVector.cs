using Hikyaku;

namespace Jigen.Core.Command.collections;

public class SetVector: IRequest
{
  public string Database { get; set; }
  public string Collection { get; set; }
  public byte[] Key { get; set; }
  public byte[] Content { get; set; }
  /// <summary>Every vector of the entry, all with the collection dimensions. A single-vector entry is an array of one.</summary>
  public float[][] Vectors { get; set; }
}