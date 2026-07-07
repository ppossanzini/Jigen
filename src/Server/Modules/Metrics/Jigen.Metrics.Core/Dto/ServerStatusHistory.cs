namespace Jigen.Metrics.Core.Dto
{
  public class ServerStatusHistory
  {
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int SampleIntervalSeconds { get; set; }
    public required IEnumerable<ServerStatusSample> Samples { get; set; }
  }

  public class ServerStatusSample
  {
    public DateTimeOffset TimestampUtc { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageBytes { get; set; }
    public required IEnumerable<DatabaseStatus> Databases { get; set; }
  }

  public class DatabaseStatus
  {
    public required string Name { get; set; }
    public long IngestionQueueLength { get; set; }
    public int CollectionsCount { get; set; }
    public int TotalElementsCount { get; set; }
    public long ContentSizeBytes { get; set; }
    public long VectorSizeBytes { get; set; }
    public long IndexSizeBytes { get; set; }
    public required IEnumerable<CollectionStatus> Collections { get; set; }
  }

  public class CollectionStatus
  {
    public required string Name { get; set; }
    public int ElementsCount { get; set; }
    public int Dimensions { get; set; }
    public long ContentSizeBytes { get; set; }
    public long VectorSizeBytes { get; set; }
    public long IndexSizeBytes { get; set; }
    public int DeletedCount { get; set; }
    public int MaxLevel { get; set; }
    public double AverageDegree { get; set; }
    public string? Quantization { get; set; }
  }
}