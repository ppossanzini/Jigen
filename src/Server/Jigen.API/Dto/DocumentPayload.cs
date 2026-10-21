namespace Jigen.API.Dto;

public class DocumentPayload
{
  public object Payload { get; set; }
  public string Sentence { get; set; }

  public SparsePayload Sparse { get; set; }
}
