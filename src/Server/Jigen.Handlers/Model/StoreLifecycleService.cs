using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jigen.Handlers.Model;

/// <summary>
/// Store lifecycle for the server:
/// - periodic durability checkpoint: SaveChangesAsync on every open database
///   (appends and deletes are group-committed — without this nothing would
///   fsync until shutdown) which also surfaces background ingestion errors;
/// - graceful shutdown: closes every store, releasing the exclusive lock
///   files so the next start is not treated as crash recovery.
/// </summary>
public class StoreLifecycleService(
  DatabasesManager manager,
  SystemDB master,
  IOptions<JigenServerSettings> settings,
  ILogger<StoreLifecycleService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var interval = settings.Value.CheckpointIntervalSeconds;
    if (interval <= 0) return; // checkpoints disabled

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
        await CheckpointAsync(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      // shutting down
    }
  }

  private async Task CheckpointAsync(CancellationToken cancellationToken)
  {
    // Snapshot: databases can be created/deleted while we iterate.
    foreach (var (name, store) in manager.ActiveDatabases.ToArray())
    {
      try
      {
        await store.SaveChangesAsync(cancellationToken);
      }
      catch (IOException ex)
      {
        // Background writer/indexer failure: this is the ONLY place the
        // server observes it — losing this log means losing data silently.
        logger.LogError(ex, "Database {Database}: background ingestion error surfaced at checkpoint", name);
      }
      catch (ObjectDisposedException)
      {
        // The database was closed/deleted between the snapshot and the flush.
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Database {Database}: checkpoint failed", name);
      }
    }
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    // Stop the checkpoint loop first.
    await base.StopAsync(cancellationToken);

    foreach (var (name, store) in manager.ActiveDatabases.ToArray())
    {
      try
      {
        // Surface any pending background ingestion error before closing:
        // this is the last chance to see it in the logs.
        await store.SaveChangesAsync(cancellationToken);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Database {Database}: background ingestion reported an error during shutdown", name);
      }

      try
      {
        await store.Close();
        logger.LogInformation("Database {Database} closed cleanly", name);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error closing database {Database}", name);
      }
    }

    try
    {
      await master.Close();
      logger.LogInformation("System database closed cleanly");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error closing the system database");
    }
  }
}
