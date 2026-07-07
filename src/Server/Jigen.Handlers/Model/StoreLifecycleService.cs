using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jigen.Handlers.Model;

/// <summary>
/// Closes every open store on graceful shutdown. Without this the exclusive
/// lock files survive the process, so every server restart would look like a
/// crash (WasUncleanShutdown) and trigger a full index reconciliation of every
/// database — and the final flushes would be skipped.
/// </summary>
public class StoreLifecycleService(
  DatabasesManager manager,
  SystemDB master,
  ILogger<StoreLifecycleService> logger) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    foreach (var (name, store) in manager.ActiveDatabases)
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
