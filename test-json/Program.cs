using System;
using System.Text.Json;
using MessagePack;

class Program { 
  static void Main() { 
    var options = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
    var el = JsonDocument.Parse("{\"foo\":\"bar\"}").RootElement;
    var bytes = MessagePackSerializer.ConvertFromJson(el.GetRawText(), options);
    var json = MessagePackSerializer.ConvertToJson(bytes, options);
    Console.WriteLine("Converted: " + json);
  } 
}
