using Hikyaku;

namespace Jigen.Core.Command.database;

/// <summary>
/// Compacts the data files, the vector index and the sparse segments of a
/// database. Returns false when a shrink of the same database is already running.
/// </summary>
public class ShrinkDatabase : IRequest<bool>
{
  public string Database { get; set; }
}
