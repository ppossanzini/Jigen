namespace Jigen.Client.BaseTypes;

/// <summary>
/// One result per shared metadata record. <see cref="MetadataIndex"/> is the
/// opaque, stable metadata identity; <see cref="VectorSearchResult{T}.Key"/>
/// and <see cref="VectorSearchResult{T}.Score"/> describe the winning embedding
/// of the group.
/// </summary>
public class MetadataSearchResult<T> : VectorSearchResult<T>
  where T : class, new()
{
  public int MetadataIndex { get; set; }
}
