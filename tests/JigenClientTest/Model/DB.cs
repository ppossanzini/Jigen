using Jigen.Client;

namespace JigenClientTest.Model;

public class DB (ConnectionOptions options) : Jigen.Client.Context(options)
{
  public VectorCollection<Entity1> Sentences => new VectorCollection<Entity1>(this);
}