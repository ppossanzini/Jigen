using Jigen;
using Jigen.Persistance;
using Xunit.Abstractions;

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

public class TestStoredList(ITestOutputHelper testOutputHelper)
{
  [Fact]
  public void CreateList()
  {
    var list = new StoredList<TestItem, TestItemOptions>(new StoreListOptions()
    {
      FilePath = "/data/test.list",
    }, new TestItemOptions());


    list.Add(new TestItem() { Id = 1, Name = "test" });
    list.Add(new TestItem() { Id = 2, Name = "test2" });

    list.Flush();

    Assert.Equal("test", list[0].Name);
    Assert.Equal("test2", list[1].Name);
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

  [Fact]
  public async Task ShrinkDbTest()
  {
    const string filePath = "/data/testshrink.list";

    testOutputHelper.WriteLine("Testintg Shrink");

    if (File.Exists(filePath)) File.Delete(filePath);
    if (File.Exists($"{filePath}.index")) File.Delete($"{filePath}.index");

    await using var list = new StoredList<TestItem, TestItemOptions>(new StoreListOptions()
    {
      FilePath = filePath,
    }, new TestItemOptions());

    testOutputHelper.WriteLine("Testintg Shrink 1");

    list.Add(new TestItem() { Id = 1, Name = "first" });
    list.Add(new TestItem() { Id = 2, Name = "second" });
    list.Flush();

    testOutputHelper.WriteLine("Testintg Shrink 2");

    var baseSize = new FileInfo(filePath).Length;

    for (var i = 1; i <= 30; i++)
    {
      list[1] = new TestItem()
      {
        Id = 2,
        Name = new string('x', i * 200),
      };
    }

    testOutputHelper.WriteLine("Testintg Shrink 3");

    list.Flush();
    var bloatedSize = new FileInfo(filePath).Length;

    testOutputHelper.WriteLine("Testintg Shrink 4");
    Assert.True(bloatedSize >= baseSize);

    var expectedFirst = list[0].Name;
    var expectedSecond = list[1].Name;

    testOutputHelper.WriteLine("Testintg Shrink 5");
    list.ShrinkDB();
    list.Flush();
    testOutputHelper.WriteLine("Testintg Shrink 6");


    var shrunkSize = new FileInfo(filePath).Length;

    Assert.True(shrunkSize < bloatedSize);
    Assert.Equal(expectedFirst, list[0].Name);
    Assert.Equal(expectedSecond, list[1].Name);
  }
}