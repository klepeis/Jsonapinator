using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Exceptions;
using Jsonapinator.Metadata;

namespace Jsonapinator.Deserialization;

/// <summary>
/// Maps a spec-shaped <see cref="ResourceObject"/> onto a POCO instance using
/// <see cref="IResourceTypeResolver"/> metadata. Mapping is presence-based: only attribute and
/// relationship keys actually present in the source JSON are written, so a JSON:API PATCH
/// payload (which omits unchanged members) can be mapped onto an already-populated target
/// without clobbering fields it didn't mention. Related resources are mapped as identifier-only
/// stubs (id + type) since compound documents are out of scope for V1.
/// </summary>
public sealed class ResourceMapper
{
    private readonly IResourceTypeResolver _resolver;
    private readonly JsonApiSerializerOptions _options;

    public ResourceMapper(IResourceTypeResolver resolver, JsonApiSerializerOptions? options = null)
    {
        _resolver = resolver;
        _options = options ?? new JsonApiSerializerOptions();
    }

    public T Map<T>(ResourceObject resource, IReadOnlyList<ResourceObject>? included = null) where T : new()
    {
        var target = new T();
        var lookup = BuildLookup(resource, included);
        var visiting = new HashSet<(string Type, string Id)>();
        MapOnto(resource, target!, lookup, visiting, depth: 0);
        return target;
    }

    /// <summary>
    /// Non-generic bridge for callers that only have a runtime <see cref="Type"/>, not a
    /// compile-time <c>T</c> (e.g. an ASP.NET Core input formatter). Prefer <see cref="Map{T}"/>
    /// when <c>T</c> is known at the call site.
    /// </summary>
    public object Map(Type targetType, ResourceObject resource, IReadOnlyList<ResourceObject>? included = null)
    {
        var target = CreateInstance(targetType);
        var lookup = BuildLookup(resource, included);
        var visiting = new HashSet<(string Type, string Id)>();
        MapOnto(resource, target, lookup, visiting, depth: 0);
        return target;
    }

    public void MapOnto(ResourceObject resource, object target) =>
        MapOnto(resource, target, BuildLookup(resource, null), new HashSet<(string Type, string Id)>(), depth: 0);

    private void MapOnto(
        ResourceObject resource,
        object target,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting,
        int depth)
    {
        var metadata = _resolver.Resolve(target.GetType());

        if (resource.Id is not null)
        {
            metadata.IdProperty.SetValue(target, ParseId(resource.Id, metadata.IdProperty.PropertyType));
        }

        if (resource.Attributes is not null)
        {
            foreach (var attribute in metadata.Attributes)
            {
                if (resource.Attributes.TryGetValue(attribute.JsonName, out var rawValue))
                {
                    attribute.Property.SetValue(
                        target, ConvertAttributeValue(rawValue, attribute.Property.PropertyType, attribute.JsonName));
                }
            }
        }

        if (resource.Relationships is not null)
        {
            foreach (var relationship in metadata.Relationships)
            {
                if (resource.Relationships.TryGetValue(relationship.Name, out var relationshipObject))
                {
                    relationship.Property.SetValue(target, BuildRelationshipValue(relationshipObject, relationship, lookup, visiting, depth));

                    if (relationship.MetaProperty is not null && relationshipObject.Meta is not null)
                    {
                        relationship.MetaProperty.SetValue(target, relationshipObject.Meta);
                    }

                    if (relationship.LinksProperty is not null && relationshipObject.Links is not null)
                    {
                        relationship.LinksProperty.SetValue(target, relationshipObject.Links);
                    }
                }
            }
        }

        if (metadata.MetaProperty is not null && resource.Meta is not null)
        {
            metadata.MetaProperty.SetValue(target, resource.Meta);
        }

        if (metadata.LinksProperty is not null && resource.Links is not null)
        {
            metadata.LinksProperty.SetValue(target, resource.Links);
        }

        if (metadata.TypeProperty is not null)
        {
            metadata.TypeProperty.SetValue(target, resource.Type);
        }
    }

    private Dictionary<(string Type, string Id), ResourceObject> BuildLookup(
        ResourceObject primary, IReadOnlyList<ResourceObject>? included)
    {
        if (included is not null && included.Count > _options.MaxIncludedResources)
        {
            throw new JsonApiMappingException(
                $"The 'included' array contains {included.Count} resources, exceeding the configured " +
                $"limit of {_options.MaxIncludedResources} ({nameof(JsonApiSerializerOptions.MaxIncludedResources)}).");
        }

        var lookup = new Dictionary<(string Type, string Id), ResourceObject>();

        void Add(ResourceObject resource)
        {
            if (resource.Id is not null)
            {
                lookup[(resource.Type, resource.Id)] = resource;
            }
        }

        Add(primary);
        if (included is not null)
        {
            foreach (var resource in included)
            {
                Add(resource);
            }
        }

        return lookup;
    }

    private static object? ConvertAttributeValue(object? rawValue, Type targetType, string attributeName)
    {
        if (rawValue is not JsonNode node)
        {
            return rawValue;
        }

        try
        {
            return node.Deserialize(targetType, NestedValueSerialization.CamelCase);
        }
        catch (JsonException ex)
        {
            throw new JsonApiMappingException(
                $"Attribute '{attributeName}' could not be converted to type '{targetType.Name}'.", ex);
        }
    }

    private object? BuildRelationshipValue(
        RelationshipObject relationshipObject,
        RelationshipMetadata relationshipMetadata,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting,
        int depth)
    {
        if (relationshipMetadata.Kind == RelationshipKind.ToMany)
        {
            var manyData = relationshipObject.ManyData ?? new List<ResourceIdentifierObject>();
            if (manyData.Count > _options.MaxToManyRelationshipSize)
            {
                throw new JsonApiMappingException(
                    $"Relationship '{relationshipMetadata.Name}' contains {manyData.Count} related resources, " +
                    $"exceeding the configured limit of {_options.MaxToManyRelationshipSize} " +
                    $"({nameof(JsonApiSerializerOptions.MaxToManyRelationshipSize)}).");
            }

            var elementType = relationshipMetadata.RelatedClrType;
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var identifier in manyData)
            {
                list.Add(BuildRelatedInstance(identifier, relationshipMetadata, lookup, visiting, depth));
            }

            var propertyType = relationshipMetadata.Property.PropertyType;
            if (propertyType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            if (propertyType.IsAssignableFrom(listType))
            {
                return list;
            }

            throw new JsonApiMappingException(
                $"Relationship property type '{propertyType.Name}' is not supported for to-many mapping; use List<T> or T[].");
        }

        return relationshipObject.SingleData is null
            ? null
            : BuildRelatedInstance(relationshipObject.SingleData, relationshipMetadata, lookup, visiting, depth);
    }

    private object BuildRelatedInstance(
        ResourceIdentifierObject identifier,
        RelationshipMetadata relationshipMetadata,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting,
        int depth)
    {
        var clrType = ResolveConcreteType(relationshipMetadata, identifier.Type);
        var instance = CreateInstance(clrType);
        var metadata = _resolver.Resolve(clrType);
        metadata.IdProperty.SetValue(instance, ParseId(identifier.Id, metadata.IdProperty.PropertyType));

        if (metadata.TypeProperty is not null)
        {
            metadata.TypeProperty.SetValue(instance, identifier.Type);
        }

        var key = (identifier.Type, identifier.Id);
        if (lookup.TryGetValue(key, out var fullResource) && visiting.Add(key))
        {
            if (depth + 1 > _options.MaxIncludeDepth)
            {
                throw new JsonApiMappingException(
                    $"Relationship hydration exceeded the configured maximum depth of " +
                    $"{_options.MaxIncludeDepth} ({nameof(JsonApiSerializerOptions.MaxIncludeDepth)}) while " +
                    $"resolving '{identifier.Type}:{identifier.Id}'.");
            }

            MapOnto(fullResource, instance, lookup, visiting, depth + 1);
            visiting.Remove(key);
        }

        return instance;
    }

    /// <summary>
    /// Resolves the concrete CLR type to instantiate for a related resource. If
    /// <see cref="RelationshipMetadata.PolymorphicDerivedTypes"/> is set (the relationship's
    /// declared <see cref="RelationshipMetadata.RelatedClrType"/> carries
    /// <see cref="System.Text.Json.Serialization.JsonPolymorphicAttribute"/>), the incoming JSON:API
    /// <paramref name="typeDiscriminator"/> (a resource identifier's own <c>"type"</c> string) is
    /// looked up against the declared <c>[JsonDerivedType]</c> registrations; otherwise the
    /// relationship's single static <see cref="RelationshipMetadata.RelatedClrType"/> is used
    /// unchanged, exactly as before polymorphic relationships were supported.
    /// </summary>
    private static Type ResolveConcreteType(RelationshipMetadata relationshipMetadata, string typeDiscriminator)
    {
        if (relationshipMetadata.PolymorphicDerivedTypes is null)
        {
            return relationshipMetadata.RelatedClrType;
        }

        return relationshipMetadata.PolymorphicDerivedTypes.TryGetValue(typeDiscriminator, out var derivedType)
            ? derivedType
            : throw new JsonApiMappingException(
                $"No [JsonDerivedType] on '{relationshipMetadata.RelatedClrType.Name}' matches JSON:API type " +
                $"'{typeDiscriminator}' for relationship '{relationshipMetadata.Name}'.");
    }

    private static object CreateInstance(Type clrType)
    {
        try
        {
            return Activator.CreateInstance(clrType)!;
        }
        catch (MissingMethodException ex)
        {
            throw new JsonApiMappingException(
                $"Related type '{clrType.Name}' must have a public parameterless constructor to be used as a relationship stub.", ex);
        }
    }

    private static object ParseId(string id, Type idType)
    {
        if (idType == typeof(string)) return id;
        if (idType == typeof(Guid)) return ParseOrThrow(id, idType, s => Guid.Parse(s));
        if (idType == typeof(int)) return ParseOrThrow(id, idType, s => int.Parse(s, CultureInfo.InvariantCulture));
        if (idType == typeof(long)) return ParseOrThrow(id, idType, s => long.Parse(s, CultureInfo.InvariantCulture));

        throw new JsonApiMappingException($"Unsupported id type '{idType.Name}'.");
    }

    private static object ParseOrThrow(string id, Type idType, Func<string, object> parse)
    {
        try
        {
            return parse(id);
        }
        catch (FormatException ex)
        {
            throw new JsonApiMappingException($"Id value '{id}' is not a valid {idType.Name}.", ex);
        }
    }
}
