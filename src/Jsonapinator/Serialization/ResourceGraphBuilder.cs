using System.Collections;
using System.Globalization;
using Jsonapinator.Attributes;
using Jsonapinator.Document;
using Jsonapinator.Metadata;

namespace Jsonapinator.Serialization;

/// <summary>
/// Converts POCOs mapped via <c>Jsonapinator.Attributes</c> into the spec-shaped
/// <see cref="Document"/> model, using <see cref="IResourceTypeResolver"/> for mapping metadata.
/// </summary>
public sealed class ResourceGraphBuilder
{
    private readonly IResourceTypeResolver _resolver;

    public ResourceGraphBuilder(IResourceTypeResolver resolver)
    {
        _resolver = resolver;
    }

    public JsonApiDocument BuildDocument(object resource) =>
        JsonApiDocument.ForSingleResource(BuildResource(resource));

    public JsonApiDocument BuildCollectionDocument(IEnumerable resources)
    {
        var resourceObjects = new List<ResourceObject>();
        foreach (var resource in resources)
        {
            resourceObjects.Add(BuildResource(resource));
        }

        return JsonApiDocument.ForCollection(resourceObjects);
    }

    public ResourceObject BuildResource(object resource)
    {
        var metadata = _resolver.Resolve(resource.GetType());

        var resourceObject = new ResourceObject
        {
            Type = metadata.ResourceType,
            Id = FormatId(metadata.IdProperty.GetValue(resource)),
        };

        if (metadata.Attributes.Count > 0)
        {
            resourceObject.Attributes = metadata.Attributes.ToDictionary(
                a => a.JsonName,
                a => a.Property.GetValue(resource));
        }

        if (metadata.Relationships.Count > 0)
        {
            resourceObject.Relationships = metadata.Relationships.ToDictionary(
                r => r.Name,
                r => BuildRelationship(resource, r));
        }

        return resourceObject;
    }

    private RelationshipObject BuildRelationship(object resource, RelationshipMetadata relationshipMetadata)
    {
        var value = relationshipMetadata.Property.GetValue(resource);

        if (relationshipMetadata.Kind == RelationshipKind.ToMany)
        {
            var identifiers = new List<ResourceIdentifierObject>();
            if (value is IEnumerable enumerable)
            {
                foreach (var element in enumerable)
                {
                    identifiers.Add(BuildIdentifier(element));
                }
            }

            return new RelationshipObject { IsToMany = true, ManyData = identifiers };
        }

        return new RelationshipObject
        {
            IsToMany = false,
            SingleData = value is null ? null : BuildIdentifier(value),
        };
    }

    private ResourceIdentifierObject BuildIdentifier(object relatedResource)
    {
        var metadata = _resolver.Resolve(relatedResource.GetType());
        return new ResourceIdentifierObject
        {
            Type = metadata.ResourceType,
            Id = FormatId(metadata.IdProperty.GetValue(relatedResource)),
        };
    }

    private static string FormatId(object? idValue) => idValue switch
    {
        null => throw new Exceptions.JsonApiMappingException("A resource's [JsonApiId] property must not be null."),
        string s => s,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => idValue.ToString()!,
    };
}
