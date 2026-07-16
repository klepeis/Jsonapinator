namespace Jsonapinator.Metadata;

/// <summary>
/// Resolves and caches <see cref="ResourceMetadata"/> for CLR types mapped to JSON:API
/// resources via <c>Jsonapinator.Attributes</c>. The DIP seam between the reflection-based
/// default implementation and the serialization/deserialization layers that consume it.
/// </summary>
public interface IResourceTypeResolver
{
    ResourceMetadata Resolve(Type clrType);
}
