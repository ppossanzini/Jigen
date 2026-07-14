using System.Reflection;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Abstractions;

/// <summary>
/// Reflects over a record type <typeparamref name="TRecord"/> once (cached
/// per closed <typeparamref name="TKey"/>/<typeparamref name="TRecord"/> pair)
/// to find its <c>[VectorStoreKey]</c> and <c>[VectorStoreVector]</c>
/// properties, and to convert between it and the (content-without-vector,
/// vector, key-bytes) triple Jigen stores.
/// </summary>
/// <remarks>
/// Jigen stores exactly one embedding per entry, so a record type must
/// declare exactly one <c>[VectorStoreVector]</c> property. Keys are
/// supported for <see cref="Guid"/> and <see cref="string"/> only (matching
/// what Jigen's own <c>VectorKey</c> converts from directly); anything else
/// throws <see cref="NotSupportedException"/> as soon as a collection over
/// that record type is built, not on first use.
/// </remarks>
public sealed class JigenRecordModel<TKey, TRecord>
  where TKey : notnull
  where TRecord : class, new()
{
  private static readonly Lazy<JigenRecordModel<TKey, TRecord>> LazyInstance = new(() => new JigenRecordModel<TKey, TRecord>());

  /// <summary>The single cached model for this TKey/TRecord pair. Reflection runs once, on first access.</summary>
  public static JigenRecordModel<TKey, TRecord> Instance => LazyInstance.Value;

  private static readonly Type[] SupportedVectorPropertyTypes =
  [
    typeof(float[]),
    typeof(ReadOnlyMemory<float>),
    typeof(Embedding<float>)
  ];

  public PropertyInfo KeyProperty { get; }
  public PropertyInfo VectorProperty { get; }

  /// <summary>Every settable public property except <see cref="VectorProperty"/>: what gets carried into the content clone Jigen persists.</summary>
  private readonly PropertyInfo[] _contentProperties;

  private JigenRecordModel()
  {
    if (typeof(TKey) != typeof(Guid) && typeof(TKey) != typeof(string))
    {
      throw new NotSupportedException(
        $"Jigen's Semantic Kernel connector only supports 'Guid' or 'string' record keys; " +
        $"'{typeof(TRecord).FullName}' uses key type '{typeof(TKey).FullName}'.");
    }

    var properties = typeof(TRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    var keyProperties = properties.Where(p => p.GetCustomAttribute<VectorStoreKeyAttribute>() is not null).ToArray();
    if (keyProperties.Length != 1)
    {
      throw new ArgumentException(
        $"Record type '{typeof(TRecord).FullName}' must declare exactly one property annotated with " +
        $"[VectorStoreKey] (found {keyProperties.Length}).");
    }

    KeyProperty = keyProperties[0];

    var vectorProperties = properties.Where(p => p.GetCustomAttribute<VectorStoreVectorAttribute>() is not null).ToArray();
    if (vectorProperties.Length != 1)
    {
      throw new ArgumentException(
        $"Record type '{typeof(TRecord).FullName}' must declare exactly one property annotated with " +
        $"[VectorStoreVector] — Jigen stores a single embedding per entry (found {vectorProperties.Length}).");
    }

    VectorProperty = vectorProperties[0];

    if (!SupportedVectorPropertyTypes.Contains(VectorProperty.PropertyType))
    {
      throw new NotSupportedException(
        $"Vector property '{VectorProperty.Name}' on '{typeof(TRecord).FullName}' has unsupported type " +
        $"'{VectorProperty.PropertyType}'. Supported types: {string.Join(", ", SupportedVectorPropertyTypes.Select(t => t.Name))}.");
    }

    if (!VectorProperty.CanRead || !VectorProperty.CanWrite)
    {
      throw new ArgumentException(
        $"Vector property '{VectorProperty.Name}' on '{typeof(TRecord).FullName}' must have both a getter and a setter.");
    }

    if (!KeyProperty.CanRead || !KeyProperty.CanWrite)
    {
      throw new ArgumentException(
        $"Key property '{KeyProperty.Name}' on '{typeof(TRecord).FullName}' must have both a getter and a setter.");
    }

    _contentProperties = properties.Where(p => p.CanRead && p.CanWrite && p != VectorProperty).ToArray();
  }

  #region Key conversion (byte[] <-> TKey)

  public byte[] KeyToBytes(TKey key)
  {
    return key switch
    {
      Guid guid => guid.ToByteArray(),
      string str => Encoding.UTF8.GetBytes(str),
      _ => throw new NotSupportedException($"Unsupported key type '{key.GetType().FullName}'.")
    };
  }

  public TKey KeyFromBytes(byte[] bytes)
  {
    if (typeof(TKey) == typeof(Guid))
      return (TKey)(object)new Guid(bytes);

    if (typeof(TKey) == typeof(string))
      return (TKey)(object)Encoding.UTF8.GetString(bytes);

    throw new NotSupportedException($"Unsupported key type '{typeof(TKey).FullName}'.");
  }

  public TKey GetKey(TRecord record) => (TKey)KeyProperty.GetValue(record)!;

  public void SetKey(TRecord record, TKey key) => KeyProperty.SetValue(record, key);

  #endregion

  #region Vector conversion

  public float[]? GetVector(TRecord record)
  {
    var value = VectorProperty.GetValue(record);
    return value switch
    {
      null => null,
      float[] array => array,
      ReadOnlyMemory<float> memory => memory.IsEmpty ? null : memory.ToArray(),
      Embedding<float> embedding => embedding.Vector.ToArray(),
      _ => throw new NotSupportedException($"Unsupported vector value type '{value.GetType().FullName}'.")
    };
  }

  public void SetVector(TRecord record, float[]? vector)
  {
    if (vector is null)
      return;

    object typedValue = VectorProperty.PropertyType switch
    {
      var t when t == typeof(float[]) => vector,
      var t when t == typeof(ReadOnlyMemory<float>) => new ReadOnlyMemory<float>(vector),
      var t when t == typeof(Embedding<float>) => new Embedding<float>(vector),
      _ => throw new NotSupportedException($"Unsupported vector property type '{VectorProperty.PropertyType}'.")
    };

    VectorProperty.SetValue(record, typedValue);
  }

  #endregion

  #region Content cloning

  /// <summary>
  /// Shallow-clones <paramref name="record"/> into a fresh <typeparamref name="TRecord"/>
  /// with every property copied except the vector, which is left at its
  /// default (so Jigen's document serializer never persists the embedding
  /// twice — once in the content bytes, once in the dedicated vector file).
  /// </summary>
  public TRecord StripVectorForStorage(TRecord record)
  {
    var clone = new TRecord();
    foreach (var property in _contentProperties)
      property.SetValue(clone, property.GetValue(record));

    return clone;
  }

  #endregion
}
