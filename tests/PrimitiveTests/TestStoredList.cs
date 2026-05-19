using Jigen;
using Jigen.Persistance;

namespace PrimitiveTests;

public class TestItemOptions
{
}

public class TestItem : IStorableItem<TestItem, TestItemOptions>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Test123 { get; set; }
    
    public ReadOnlyMemory<byte> Serialize()
    {
     return MessagePackDocumentSerializer.Instance.Serialize(this);
    }

    public static TestItem Deserialize(ReadOnlyMemory<byte> data, TestItemOptions options)
    {
        return MessagePackDocumentSerializer.Instance.Deserialize<TestItem>(data);
    }
}

public class TestStoredList
{
    [Fact]
    public void CreateList()
    {
        var list = new StoredList<TestItem, TestItemOptions>(new StoreListOptions()
        {
            FilePath = "/data/test.list",
        }, new TestItemOptions());
        
        
        list.Add(new TestItem(){ Id = 1, Name = "test"});
        list.Add(new TestItem(){ Id = 2, Name = "test2"});
        
        list.Flush();

        Assert.Equal("test",list[0].Name);
        Assert.Equal("test2",list[1].Name);
    }
    
    
    [Fact]
    public void SizeTest()
    {
        var list = new StoredList<TestItem, TestItemOptions>(new StoreListOptions()
        {
            FilePath = "/data/testsize.list",
        }, new TestItemOptions());


        var item1 = new TestItem() { Id = 1, Name = "test" };
        var item2 = new TestItem() { Id = 1, Name = "test" }; 
        
        list.Add(item1);
        list.Add(item2);
        
        list.Flush();

        
        var size = new FileInfo("/data/testsize.list").Length;

        for (var i = 0; i < 100; i++)
        {
            item2.Id = i;
            list[1] = item2;
        }
        
        list.Flush();
        
        var size2 = new FileInfo("/data/testsize.list").Length;

        Assert.Equal(size, size2);
    }
}