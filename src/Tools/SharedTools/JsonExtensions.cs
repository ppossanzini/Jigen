using System.Globalization;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;


/// <summary>
/// Adds default JSON serialization/deserialization methods for all objects.
/// </summary>
public static class JsonExtensions
{
  /// <summary>
  /// Deserializes the JSON Object string to a concrete object of type <typeparamref name="T"/>.
  /// </summary>
  /// <param name="jsonString">The JSON string to deserialize.</param>
  /// <param name="settings"></param>
  /// <typeparam name="T">The type of the deserialized object.</typeparam>
  /// <returns></returns>
  public static T ToJsonObject<T>(this string jsonString,JsonSerializerSettings settings = null)
  {
    return JsonConvert.DeserializeObject<T>(jsonString, settings ?? new JsonSerializerSettings().ConfigureDefaults());
  }

  /// <summary>
  /// Serializes <paramref name="obj"/> to a JSON string.
  /// </summary>
  /// <param name="obj">The object to serialize.</param>
  /// <param name="settings"></param>
  /// <returns>The JSON string representation of <paramref name="obj"/>.</returns>
  public static string ToJsonString(this object obj, JsonSerializerSettings settings = null)
  {
    return JsonConvert.SerializeObject(obj, settings ?? new JsonSerializerSettings().ConfigureDefaults());
  }

  public static JsonSerializerSettings ConfigureDefaults(this JsonSerializerSettings settings)
  {
    settings.Culture = CultureInfo.InvariantCulture;
    settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    settings.MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore;
    settings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    settings.DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat;
    settings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
    return settings;
  }
}
