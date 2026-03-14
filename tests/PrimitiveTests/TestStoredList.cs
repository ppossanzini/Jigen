using Jigen;
using Jigen.Persistance;

namespace PrimitiveTests;

public class TestItem : IStorableItem<TestItem>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Test123 { get; set; }
    
    public ReadOnlyMemory<byte> Serialize()
    {
     return MessagePackDocumentSerializer.Instance.Serialize(this);
    }

    public static TestItem Deserialize(ReadOnlyMemory<byte> data)
    {
        return MessagePackDocumentSerializer.Instance.Deserialize<TestItem>(data);
    }
}

public class TestStoredList
{
    [Fact]
    public void CreateList()
    {
        var list = new StoredList<TestItem>(new StoreListOptions()
        {
            FilePath = "/data/test.list",
        });
        
        
        list.Add(new TestItem(){ Id = 1, Name = "test"});
        list.Add(new TestItem(){ Id = 2, Name = "test2"});
        
        list.Flush();
    }
}