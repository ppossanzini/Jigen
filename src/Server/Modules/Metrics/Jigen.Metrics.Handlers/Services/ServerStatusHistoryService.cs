using System.Diagnostics;
using Jigen.Handlers.Model;
using Jigen.Metrics.Core.Dto;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jigen.Metrics.Handlers.Services;

public sealed class ServerStatusHistoryService(
  DatabasesManager databasesManager,
  ILogger<ServerStatusHistoryService> logger) : IHostedService, IDisposable
{
  private const int MaxSamples = 720;
  private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
  private static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(1);

  private readonly Lock _samplesLock = new();
  private readonly List<ServerStatusSample> _samples = [];

  private CancellationTokenSource? _stoppingTokenSource;
  private Task? _backgroundTask;
  private TimeSpan _lastTotalProcessorTime;
  private DateTimeOffset _lastTimestampUtc;
  private bool _hasPreviousCpuSample;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    CaptureSample(DateTimeOffset.UtcNow);

    _stoppingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _backgroundTask = RunSamplingLoopAsync(_stoppingTokenSource.Token);
    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    if (_stoppingTokenSource is null)
      return;

    await _stoppingTokenSource.CancelAsync();

    if (_backgroundTask is not null)
      await _backgroundTask.WaitAsync(cancellationToken);
  }

  public Task<ServerStatusHistory> GetHistoryAsync(TimeSpan requestedWindow, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var effectiveWindow = NormalizeWindow(requestedWindow);

    ServerStatusSample[] samples;
    lock (_samplesLock)
    {
      samples = _samples.ToArray();
    }

    var toUtc = samples.Length > 0 ? samples[^1].TimestampUtc : DateTimeOffset.UtcNow;
    var fromUtc = toUtc - effectiveWindow;
    var filteredSamples = samples.Where(sample => sample.TimestampUtc >= fromUtc).ToArray();

    return Task.FromResult(new ServerStatusHistory
    {
      FromUtc = fromUtc,
      ToUtc = toUtc,
      SampleIntervalSeconds = (int)SampleInterval.TotalSeconds,
      Samples = filteredSamples
    });
  }

  public void Dispose()
  {
    _stoppingTokenSource?.Dispose();
  }

  private async Task RunSamplingLoopAsync(CancellationToken cancellationToken)
  {
    using var timer = new PeriodicTimer(SampleInterval);

    try
    {
      while (await timer.WaitForNextTickAsync(cancellationToken))
        CaptureSample(DateTimeOffset.UtcNow);
    }
    catch (OperationCanceledException)
    {
    }
  }

  private void CaptureSample(DateTimeOffset timestampUtc)
  {
    try
    {
      using var process = Process.GetCurrentProcess();
      process.Refresh();

      var sample = new ServerStatusSample
      {
        TimestampUtc = timestampUtc,
        CpuUsagePercent = CalculateCpuUsage(process.TotalProcessorTime, timestampUtc),
        MemoryUsageBytes = process.WorkingSet64,
        Databases = databasesManager.ActiveDatabases
          .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
          .Select(entry => BuildDatabaseStatus(entry.Key, entry.Value))
          .ToArray()
      };

      lock (_samplesLock)
      {
        _samples.Add(sample);

        var cutoff = timestampUtc - HistoryWindow;
        _samples.RemoveAll(item => item.TimestampUtc < cutoff);

        if (_samples.Count > MaxSamples)
          _samples.RemoveRange(0, _samples.Count - MaxSamples);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Unable to capture server status sample.");
    }
  }

  private double CalculateCpuUsage(TimeSpan totalProcessorTime, DateTimeOffset timestampUtc)
  {
    if (!_hasPreviousCpuSample)
    {
      _lastTotalProcessorTime = totalProcessorTime;
      _lastTimestampUtc = timestampUtc;
      _hasPreviousCpuSample = true;
      return 0;
    }

    var elapsed = timestampUtc - _lastTimestampUtc;
    var cpuDelta = totalProcessorTime - _lastTotalProcessorTime;

    _lastTimestampUtc = timestampUtc;
    _lastTotalProcessorTime = totalProcessorTime;

    if (elapsed <= TimeSpan.Zero)
      return 0;

    var cpuUsage = cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100d;
    return Math.Clamp(cpuUsage, 0, 100);
  }

  private static DatabaseStatus BuildDatabaseStatus(string databaseName, Store store)
  {
    var collections = store.GetCollections()
      .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
      .Select(name =>
      {
        var info = store.GetCollectionInfo(name);
        return new CollectionStatus
        {
          Name = info.Name,
          ElementsCount = info.Vectors,
          Dimensions = info.Dimensions,
          ContentSizeBytes = info.ContentSize,
          VectorSizeBytes = info.VectorSize,
          IndexSizeBytes = info.Index?.IndexSizeBytes ?? 0,
          DeletedCount = info.Index?.DeletedNodes ?? 0,
          MaxLevel = info.Index?.MaxLevel ?? 0,
          AverageDegree = info.Index?.AverageDegree ?? 0,
          Quantization = info.Index?.Quantization
        };
      })
      .ToArray();

    return new DatabaseStatus
    {
      Name = databaseName,
      IngestionQueueLength = store.IngestionQueueLength,
      CollectionsCount = collections.Length,
      TotalElementsCount = collections.Sum(collection => collection.ElementsCount),
      ContentSizeBytes = collections.Sum(collection => collection.ContentSizeBytes),
      VectorSizeBytes = collections.Sum(collection => collection.VectorSizeBytes),
      IndexSizeBytes = collections.Sum(collection => collection.IndexSizeBytes),
      Collections = collections
    };
  }

  private static TimeSpan NormalizeWindow(TimeSpan requestedWindow)
  {
    if (requestedWindow == TimeSpan.FromMinutes(1))
      return requestedWindow;

    if (requestedWindow == TimeSpan.FromMinutes(5))
      return requestedWindow;

    if (requestedWindow == TimeSpan.FromMinutes(10))
      return requestedWindow;

    if (requestedWindow == TimeSpan.FromHours(1))
      return requestedWindow;

    return HistoryWindow;
  }
}