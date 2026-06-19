using System;
using System.Text.Json;
class Program { 
  static void Main() { 
    try {
        var el = JsonDocument.Parse("").RootElement.Clone();
        Console.WriteLine(el.ToString());
    } catch(Exception e) {
        Console.WriteLine("Parse empty: " + e.Message);
    }
  } 
}
