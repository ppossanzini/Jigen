using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Abstractions;

/// <summary>
/// Bridges <see cref="VectorStore.GetCollection{TKey,TRecord}"/> — whose
/// override signature is fixed to <c>where TRecord : class</c> by the base
/// SDK class — to the concrete Jigen collection types, which additionally
/// require <c>new()</c> because both <c>Jigen.VectorCollection&lt;T&gt;</c>
/// and <c>Jigen.Client.VectorCollection&lt;T&gt;</c> do. An override cannot
/// add that constraint back, so this constructs the closed generic type via
/// reflection instead, turning a missing parameterless constructor into a
/// clear <see cref="NotSupportedException"/> at collection-construction time
/// rather than a compiler error application code never sees.
/// </summary>
public static class JigenGenericCollectionFactory
{
  public static VectorStoreCollection<TKey, TRecord> Create<TKey, TRecord>(Type openCollectionType, params object[] constructorArgs)
    where TKey : notnull
    where TRecord : class
  {
    Type closedType;
    try
    {
      closedType = openCollectionType.MakeGenericType(typeof(TKey), typeof(TRecord));
    }
    catch (ArgumentException ex)
    {
      throw new NotSupportedException(
        $"Record type '{typeof(TRecord).FullName}' must have a public parameterless constructor to be used " +
        "with the Jigen Semantic Kernel connector (Jigen's own collection types require it).", ex);
    }

    try
    {
      return (VectorStoreCollection<TKey, TRecord>)Activator.CreateInstance(closedType, constructorArgs)!;
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
      // Unwrap so validation errors thrown by the collection's constructor
      // (e.g. JigenRecordModel's "exactly one [VectorStoreVector]" check)
      // surface with their own type and message instead of being wrapped.
      ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
      throw; // unreachable, keeps the compiler happy
    }
  }
}
