using Jigen.Client;

namespace JigenClientTest.Model;

public class DB : Jigen.Client.Context
{
  public VectorCollection<Entity1> Sentences;

  public DB(ConnectionOptions options) : base(options)
  {
    Sentences = new VectorCollection<Entity1>(this);
  }
}