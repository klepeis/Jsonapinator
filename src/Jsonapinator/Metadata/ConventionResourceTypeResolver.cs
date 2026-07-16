using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Exceptions;

namespace Jsonapinator.Metadata;

/// <summary>
/// Convention-based <see cref="IResourceTypeResolver"/> — requires no <c>Jsonapinator.Attributes</c>
/// on the mapped type. A property named "Id" (of a supported id type) becomes the resource id;
/// a property whose type (or element type, for collections) is itself a class with its own usable
/// "Id" property becomes a relationship (to-one, or to-many for `IEnumerable&lt;T&gt;`); every
/// other gettable+settable property becomes a flat attribute. See README for the full rule.
/// </summary>
public sealed class ConventionResourceTypeResolver : IResourceTypeResolver
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
        var mappableProperties = GetMappableProperties(clrType);

        var idProperty = mappableProperties.SingleOrDefault(p => p.Name == "Id" && SupportedIdTypes.Contains(p.PropertyType))
            ?? throw new JsonApiMappingException(
                $"Type '{clrType.Name}' must have a public settable property named 'Id' of a supported type " +
                $"({string.Join(", ", SupportedIdTypes.Select(t => t.Name))}) to be used with convention-based mapping.");

        var attributes = new List<AttributeMetadata>();
        var relationships = new List<RelationshipMetadata>();

        foreach (var property in mappableProperties)
        {
            if (property == idProperty)
            {
                continue;
            }

            Type? elementType = null;
            var isToMany = property.PropertyType != typeof(string) && TryGetEnumerableElementType(property.PropertyType, out elementType);
            var candidateType = isToMany ? elementType! : property.PropertyType;

            if (IsRelationshipTarget(candidateType))
            {
                relationships.Add(new RelationshipMetadata
                {
                    Property = property,
                    Name = PropertyNaming.ToCamelCase(property.Name),
                    Kind = isToMany ? RelationshipKind.ToMany : RelationshipKind.ToOne,
                    RelatedClrType = candidateType,
                });
            }
            else
            {
                attributes.Add(new AttributeMetadata
                {
                    Property = property,
                    JsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? PropertyNaming.ToCamelCase(property.Name),
                });
            }
        }

        return new ResourceMetadata
        {
            ClrType = clrType,
            ResourceType = PropertyNaming.ToCamelCase(clrType.Name),
            IdProperty = idProperty,
            Attributes = attributes,
            Relationships = relationships,
        };
    }

    private static List<PropertyInfo> GetMappableProperties(Type clrType) =>
        clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0
                && p.GetMethod is { IsPublic: true }
                && p.SetMethod is { IsPublic: true })
            .ToList();

    private static bool IsRelationshipTarget(Type candidateType) =>
        candidateType.IsClass
        && candidateType != typeof(string)
        && GetMappableProperties(candidateType).Any(p => p.Name == "Id" && SupportedIdTypes.Contains(p.PropertyType));

    private static bool TryGetEnumerableElementType(Type propertyType, out Type? elementType)
    {
        var enumerableInterface = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? propertyType
            : propertyType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        elementType = enumerableInterface?.GetGenericArguments()[0];
        return elementType is not null;
    }
}
