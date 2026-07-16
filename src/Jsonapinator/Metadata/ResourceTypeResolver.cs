using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Exceptions;

namespace Jsonapinator.Metadata;

/// <summary>
/// Default <see cref="IResourceTypeResolver"/> — reflects a CLR type once and caches the
/// resulting <see cref="ResourceMetadata"/>.
/// </summary>
public sealed class ResourceTypeResolver : IResourceTypeResolver
{
    private static readonly HashSet<Type> SupportedIdTypes = new()
    {
        typeof(string), typeof(Guid), typeof(int), typeof(long),
    };

    private readonly ConcurrentDictionary<Type, ResourceMetadata> _cache = new();

    public ResourceMetadata Resolve(Type clrType) =>
        _cache.GetOrAdd(clrType, Build);

    private static ResourceMetadata Build(Type clrType)
    {
        var resourceAttribute = clrType.GetCustomAttribute<JsonApiResourceAttribute>()
            ?? throw new JsonApiMappingException(
                $"Type '{clrType.Name}' must be decorated with [JsonApiResource] to be used with Jsonapinator.");

        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var idProperty = properties.SingleOrDefault(p => p.IsDefined(typeof(JsonApiIdAttribute)))
            ?? throw new JsonApiMappingException(
                $"Type '{clrType.Name}' must have exactly one property decorated with [JsonApiId].");

        if (!SupportedIdTypes.Contains(idProperty.PropertyType))
        {
            throw new JsonApiMappingException(
                $"Property '{clrType.Name}.{idProperty.Name}' has unsupported id type '{idProperty.PropertyType.Name}'. " +
                $"Supported id types are: {string.Join(", ", SupportedIdTypes.Select(t => t.Name))}.");
        }

        var attributes = properties
            .Where(p => p.IsDefined(typeof(JsonApiAttributeAttribute)))
            .Select(p => new AttributeMetadata
            {
                Property = p,
                JsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ToCamelCase(p.Name),
            })
            .ToList();

        var relationships = properties
            .Where(p => p.IsDefined(typeof(JsonApiRelationshipAttribute)))
            .Select(p =>
            {
                var relationshipAttribute = p.GetCustomAttribute<JsonApiRelationshipAttribute>()!;
                return new RelationshipMetadata
                {
                    Property = p,
                    Name = relationshipAttribute.Name,
                    Kind = relationshipAttribute.Kind,
                    RelatedClrType = relationshipAttribute.Kind == RelationshipKind.ToMany
                        ? GetElementType(p.PropertyType, clrType, p.Name)
                        : p.PropertyType,
                };
            })
            .ToList();

        return new ResourceMetadata
        {
            ClrType = clrType,
            ResourceType = resourceAttribute.ResourceType,
            IdProperty = idProperty,
            Attributes = attributes,
            Relationships = relationships,
        };
    }

    private static Type GetElementType(Type propertyType, Type declaringType, string propertyName)
    {
        if (propertyType != typeof(string))
        {
            var enumerableInterface = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? propertyType
                : propertyType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableInterface is not null)
            {
                return enumerableInterface.GetGenericArguments()[0];
            }
        }

        throw new JsonApiMappingException(
            $"Property '{declaringType.Name}.{propertyName}' is marked as a to-many relationship but its " +
            $"type '{propertyType.Name}' does not implement IEnumerable<T>.");
    }

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
