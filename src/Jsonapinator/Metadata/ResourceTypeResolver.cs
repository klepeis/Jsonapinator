using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
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

        var idCandidates = properties.Where(p => p.IsDefined(typeof(JsonApiIdAttribute))).ToList();
        if (idCandidates.Count > 1)
        {
            throw new JsonApiMappingException(
                $"Type '{clrType.Name}' has {idCandidates.Count} properties decorated with " +
                "[JsonApiId]; exactly one is required.");
        }

        var idProperty = idCandidates.SingleOrDefault()
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
                JsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? PropertyNaming.ToCamelCase(p.Name),
                IsPolymorphic = PolymorphismSupport.IsPolymorphic(p.PropertyType),
            })
            .ToList();

        var relationships = properties
            .Where(p => p.IsDefined(typeof(JsonApiRelationshipAttribute)))
            .Select(p =>
            {
                var relationshipAttribute = p.GetCustomAttribute<JsonApiRelationshipAttribute>()!;
                var relatedClrType = relationshipAttribute.Kind == RelationshipKind.ToMany
                    ? GetElementType(p.PropertyType, clrType, p.Name)
                    : p.PropertyType;
                return new RelationshipMetadata
                {
                    Property = p,
                    Name = relationshipAttribute.Name,
                    Kind = relationshipAttribute.Kind,
                    RelatedClrType = relatedClrType,
                    PolymorphicDerivedTypes = PolymorphismSupport.ResolveDerivedTypes(relatedClrType),
                };
            })
            .ToList();

        var metaProperty = FindSingleTypedProperty<JsonApiMetaAttribute>(properties, clrType, typeof(MetaObject));
        var linksProperty = FindSingleTypedProperty<JsonApiLinksAttribute>(properties, clrType, typeof(LinksObject));
        var typeProperty = FindSingleTypedProperty<JsonApiTypeAttribute>(properties, clrType, typeof(string));

        relationships = ApplyRelationshipMetaAndLinks(properties, clrType, relationships);

        return new ResourceMetadata
        {
            ClrType = clrType,
            ResourceType = resourceAttribute.ResourceType,
            IdProperty = idProperty,
            Attributes = attributes,
            Relationships = relationships,
            MetaProperty = metaProperty,
            LinksProperty = linksProperty,
            TypeProperty = typeProperty,
        };
    }

    private static PropertyInfo? FindSingleTypedProperty<TAttribute>(PropertyInfo[] properties, Type clrType, Type requiredType)
        where TAttribute : Attribute
    {
        var candidates = properties.Where(p => p.IsDefined(typeof(TAttribute))).ToList();

        if (candidates.Count > 1)
        {
            throw new JsonApiMappingException(
                $"Type '{clrType.Name}' must have at most one property decorated with [{typeof(TAttribute).Name.Replace("Attribute", "")}].");
        }

        var property = candidates.SingleOrDefault();
        if (property is null)
        {
            return null;
        }

        if (property.PropertyType != requiredType)
        {
            throw new JsonApiMappingException(
                $"Property '{clrType.Name}.{property.Name}' is decorated with [{typeof(TAttribute).Name.Replace("Attribute", "")}] " +
                $"but its type is '{property.PropertyType.Name}'; it must be exactly '{requiredType.Name}'.");
        }

        return property;
    }

    private static List<RelationshipMetadata> ApplyRelationshipMetaAndLinks(
        PropertyInfo[] properties, Type clrType, List<RelationshipMetadata> relationships)
    {
        var result = new List<RelationshipMetadata>(relationships.Count);

        foreach (var relationship in relationships)
        {
            var metaProperty = FindRelationshipTypedProperty<JsonApiRelationshipMetaAttribute>(
                properties, clrType, relationship.Name, typeof(MetaObject), a => a.RelationshipName);
            var linksProperty = FindRelationshipTypedProperty<JsonApiRelationshipLinksAttribute>(
                properties, clrType, relationship.Name, typeof(LinksObject), a => a.RelationshipName);

            result.Add(new RelationshipMetadata
            {
                Property = relationship.Property,
                Name = relationship.Name,
                Kind = relationship.Kind,
                RelatedClrType = relationship.RelatedClrType,
                MetaProperty = metaProperty,
                LinksProperty = linksProperty,
                PolymorphicDerivedTypes = relationship.PolymorphicDerivedTypes,
            });
        }

        var relationshipNames = new HashSet<string>(relationships.Select(r => r.Name));
        ValidateRelationshipReferences<JsonApiRelationshipMetaAttribute>(properties, clrType, relationshipNames, a => a.RelationshipName);
        ValidateRelationshipReferences<JsonApiRelationshipLinksAttribute>(properties, clrType, relationshipNames, a => a.RelationshipName);

        return result;
    }

    private static PropertyInfo? FindRelationshipTypedProperty<TAttribute>(
        PropertyInfo[] properties, Type clrType, string relationshipName, Type requiredType, Func<TAttribute, string> getRelationshipName)
        where TAttribute : Attribute
    {
        var candidates = properties
            .Where(p => p.IsDefined(typeof(TAttribute)) && getRelationshipName(p.GetCustomAttribute<TAttribute>()!) == relationshipName)
            .ToList();

        if (candidates.Count > 1)
        {
            throw new JsonApiMappingException(
                $"Type '{clrType.Name}' must have at most one property decorated with " +
                $"[{typeof(TAttribute).Name.Replace("Attribute", "")}(\"{relationshipName}\")].");
        }

        var property = candidates.SingleOrDefault();
        if (property is null)
        {
            return null;
        }

        if (property.PropertyType != requiredType)
        {
            throw new JsonApiMappingException(
                $"Property '{clrType.Name}.{property.Name}' is decorated with " +
                $"[{typeof(TAttribute).Name.Replace("Attribute", "")}(\"{relationshipName}\")] but its type is " +
                $"'{property.PropertyType.Name}'; it must be exactly '{requiredType.Name}'.");
        }

        return property;
    }

    private static void ValidateRelationshipReferences<TAttribute>(
        PropertyInfo[] properties, Type clrType, HashSet<string> relationshipNames, Func<TAttribute, string> getRelationshipName)
        where TAttribute : Attribute
    {
        foreach (var property in properties.Where(p => p.IsDefined(typeof(TAttribute))))
        {
            var relationshipName = getRelationshipName(property.GetCustomAttribute<TAttribute>()!);
            if (!relationshipNames.Contains(relationshipName))
            {
                throw new JsonApiMappingException(
                    $"Property '{clrType.Name}.{property.Name}' is decorated with " +
                    $"[{typeof(TAttribute).Name.Replace("Attribute", "")}(\"{relationshipName}\")] but no relationship named " +
                    $"'{relationshipName}' exists on '{clrType.Name}'.");
            }
        }
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

}
