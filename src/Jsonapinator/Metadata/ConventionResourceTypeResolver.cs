using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
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

        var metaProperty = mappableProperties.SingleOrDefault(p => p.Name == "Meta" && p.PropertyType == typeof(MetaObject));
        var linksProperty = mappableProperties.SingleOrDefault(p => p.Name == "Links" && p.PropertyType == typeof(LinksObject));
        var typeProperty = mappableProperties.SingleOrDefault(p => p.Name == "Type" && p.PropertyType == typeof(string));

        var excluded = new HashSet<PropertyInfo> { idProperty };
        if (metaProperty is not null) excluded.Add(metaProperty);
        if (linksProperty is not null) excluded.Add(linksProperty);
        if (typeProperty is not null) excluded.Add(typeProperty);

        var relationshipCandidates = new List<(PropertyInfo Property, string Name, RelationshipKind Kind, Type RelatedClrType)>();
        var attributeCandidates = new List<PropertyInfo>();

        foreach (var property in mappableProperties)
        {
            if (excluded.Contains(property))
            {
                continue;
            }

            Type? elementType = null;
            var isToMany = property.PropertyType != typeof(string) && TryGetEnumerableElementType(property.PropertyType, out elementType);
            var candidateType = isToMany ? elementType! : property.PropertyType;

            if (IsRelationshipTarget(candidateType))
            {
                relationshipCandidates.Add((
                    property,
                    PropertyNaming.ToCamelCase(property.Name),
                    isToMany ? RelationshipKind.ToMany : RelationshipKind.ToOne,
                    candidateType));
            }
            else
            {
                attributeCandidates.Add(property);
            }
        }

        var siblingExclusions = new HashSet<PropertyInfo>();
        var relationships = new List<RelationshipMetadata>();

        foreach (var candidate in relationshipCandidates)
        {
            var relMeta = attributeCandidates.FirstOrDefault(
                p => p.Name == candidate.Property.Name + "Meta" && p.PropertyType == typeof(MetaObject));
            var relLinks = attributeCandidates.FirstOrDefault(
                p => p.Name == candidate.Property.Name + "Links" && p.PropertyType == typeof(LinksObject));

            if (relMeta is not null) siblingExclusions.Add(relMeta);
            if (relLinks is not null) siblingExclusions.Add(relLinks);

            relationships.Add(new RelationshipMetadata
            {
                Property = candidate.Property,
                Name = candidate.Name,
                Kind = candidate.Kind,
                RelatedClrType = candidate.RelatedClrType,
                MetaProperty = relMeta,
                LinksProperty = relLinks,
                PolymorphicDerivedTypes = PolymorphismSupport.ResolveDerivedTypes(candidate.RelatedClrType),
            });
        }

        var attributes = attributeCandidates
            .Where(p => !siblingExclusions.Contains(p))
            .Select(p => new AttributeMetadata
            {
                Property = p,
                JsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? PropertyNaming.ToCamelCase(p.Name),
                IsPolymorphic = PolymorphismSupport.IsPolymorphic(p.PropertyType),
            })
            .ToList();

        return new ResourceMetadata
        {
            ClrType = clrType,
            ResourceType = PropertyNaming.ToCamelCase(clrType.Name),
            IdProperty = idProperty,
            Attributes = attributes,
            Relationships = relationships,
            MetaProperty = metaProperty,
            LinksProperty = linksProperty,
            TypeProperty = typeProperty,
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
