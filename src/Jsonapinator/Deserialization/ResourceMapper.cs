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

    public T Map<T>(ResourceObject resource) where T : new()
    {
        var target = new T();
        MapOnto(resource, target!);
        return target;
    }

    public void MapOnto(ResourceObject resource, object target)
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
                    relationship.Property.SetValue(target, BuildRelationshipValue(relationshipObject, relationship));
                }
            }
        }
    }

    private static object? ConvertAttributeValue(object? rawValue, Type targetType) =>
        rawValue is JsonNode node ? node.Deserialize(targetType) : rawValue;

    private object? BuildRelationshipValue(RelationshipObject relationshipObject, RelationshipMetadata relationshipMetadata)
    {
        if (relationshipMetadata.Kind == RelationshipKind.ToMany)
        {
            var elementType = relationshipMetadata.RelatedClrType;
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var identifier in relationshipObject.ManyData ?? new List<ResourceIdentifierObject>())
            {
                list.Add(BuildStub(identifier, elementType));
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
            : BuildStub(relationshipObject.SingleData, relationshipMetadata.RelatedClrType);
    }

    private object BuildStub(ResourceIdentifierObject identifier, Type clrType)
    {
        object instance;
        try
        {
            instance = Activator.CreateInstance(clrType)!;
        }
        catch (MissingMethodException ex)
        {
            throw new JsonApiMappingException(
                $"Related type '{clrType.Name}' must have a public parameterless constructor to be used as a relationship stub.", ex);
        }

        var metadata = _resolver.Resolve(clrType);
        metadata.IdProperty.SetValue(instance, ParseId(identifier.Id, metadata.IdProperty.PropertyType));
        return instance;
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
