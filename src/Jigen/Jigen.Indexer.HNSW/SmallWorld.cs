using System.Buffers;
using System.IO.MemoryMappedFiles;
using Jigen.Extensions;

namespace Jigen.Indexer;

public class SmallWorld
{
  internal MemoryMappedFile GraphData;

  // Array of tumbstoned nodes, used for efficient node removal and space reuse.
  // Save disk position of thumbstoned nodes, (nodes are all of same size) 
  ArrayPool<long> _tumbstonedNodes = ArrayPool<long>.Shared;

  // FileStream only for writings. 
  internal FileStream GraphFileStream;

  public SmallWorld(SmallWorldOptions options)
  {
    GraphFileStream = File.Open(options.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

    if (GraphFileStream.Length == 0)
    {
      GraphFileStream.Seek(0, SeekOrigin.End);
      GraphFileStream.WriteInt64Le(GraphFileStream.Position);
      GraphFileStream.Flush(true);
    }

    GraphData = MemoryMappedFile.CreateFromFile(File.Open(options.FileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite),
      null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
  }
}