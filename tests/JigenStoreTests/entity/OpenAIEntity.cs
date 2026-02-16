using Parquet.Serialization.Attributes;

namespace JigenTests.entity;

public class OpenAIEntity
{
  public string _id { get; set; }
  public string title { get; set; }
  public string text { get; set; }
  
  public double[] item { get; set; }
}