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

    public ResourceMapper(IResourceTypeResolver resolver)
    {
        _resolver = resolver;
    }

    public T Map<T>(ResourceObject resource, IReadOnlyList<ResourceObject>? included = null) where T : new()
    {
        var target = new T();
        var lookup = BuildLookup(resource, included);
        var visiting = new HashSet<(string Type, string Id)>();
        MapOnto(resource, target!, lookup, visiting);
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
        MapOnto(resource, target, lookup, visiting);
        return target;
    }

    public void MapOnto(ResourceObject resource, object target) =>
        MapOnto(resource, target, BuildLookup(resource, null), new HashSet<(string Type, string Id)>());

    private void MapOnto(
        ResourceObject resource,
        object target,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting)
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
                    attribute.Property.SetValue(target, ConvertAttributeValue(rawValue, attribute.Property.PropertyType));
                }
            }
        }

        if (resource.Relationships is not null)
        {
            foreach (var relationship in metadata.Relationships)
            {
                if (resource.Relationships.TryGetValue(relationship.Name, out var relationshipObject))
                {
                    relationship.Property.SetValue(target, BuildRelationshipValue(relationshipObject, relationship, lookup, visiting));
                }
            }
        }
    }

    private static Dictionary<(string Type, string Id), ResourceObject> BuildLookup(
        ResourceObject primary, IReadOnlyList<ResourceObject>? included)
    {
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

    private static object? ConvertAttributeValue(object? rawValue, Type targetType) =>
        rawValue is JsonNode node ? node.Deserialize(targetType) : rawValue;

    private object? BuildRelationshipValue(
        RelationshipObject relationshipObject,
        RelationshipMetadata relationshipMetadata,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting)
    {
        if (relationshipMetadata.Kind == RelationshipKind.ToMany)
        {
            var elementType = relationshipMetadata.RelatedClrType;
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var identifier in relationshipObject.ManyData ?? new List<ResourceIdentifierObject>())
            {
                list.Add(BuildRelatedInstance(identifier, elementType, lookup, visiting));
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
            : BuildRelatedInstance(relationshipObject.SingleData, relationshipMetadata.RelatedClrType, lookup, visiting);
    }

    private object BuildRelatedInstance(
        ResourceIdentifierObject identifier,
        Type clrType,
        IReadOnlyDictionary<(string Type, string Id), ResourceObject> lookup,
        HashSet<(string Type, string Id)> visiting)
    {
        var instance = CreateInstance(clrType);
        var metadata = _resolver.Resolve(clrType);
        metadata.IdProperty.SetValue(instance, ParseId(identifier.Id, metadata.IdProperty.PropertyType));

        var key = (metadata.ResourceType, identifier.Id);
        if (lookup.TryGetValue(key, out var fullResource) && visiting.Add(key))
        {
            MapOnto(fullResource, instance, lookup, visiting);
            visiting.Remove(key);
        }

        return instance;
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
