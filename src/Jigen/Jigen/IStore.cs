using Jigen.DataStructures;

namespace Jigen;

public interface IStore
{
  Task SaveChangesAsync(CancellationToken? cancellationToken);

  Task Close();
}