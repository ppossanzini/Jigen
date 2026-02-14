using Jigen.DataStructures;

namespace Jigen.Core.Dto.database;

public class DatabaseInfo
{
  public string Name { get; set; }
  public int Vectors { get; set; }

  public long ContentSize => Collections.Sum(c => c.ContentSize);
  public long VectorSize => Collections.Sum(c => c.VectorSize);

  public long AllocatedContentSize { get; set; }
  public long AllocatedVectorSize { get; set; }

  public long ContentFreeSpace => AllocatedContentSize - ContentSize;
  public long VectorFreeSpace => AllocatedVectorSize - VectorSize;

  public IEnumerable<CollectionInfo> Collections { get; set; }
}