using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonapinator.Document;
using Jsonapinator.Exceptions;

namespace Jsonapinator.Deserialization;

/// <summary>
/// Default <see cref="IJsonApiReader"/> — parses a JSON:API document into the spec-shaped
/// <see cref="Document"/> model. Structural parse only; resource-to-POCO mapping is handled
/// separately by <see cref="ResourceMapper"/>.
/// </summary>
public sealed class JsonApiDocumentReader : IJsonApiReader
{
    public JsonApiDocument Read(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new JsonApiMappingException("The input is not valid JSON.", ex);
        }

        if (root is not JsonObject obj)
        {
            throw new JsonApiMappingException("A JSON:API document must be a JSON object.");
        }

        var document = new JsonApiDocument();

        if (obj.TryGetPropertyValue("data", out var dataNode))
        {
            document.Data = ReadData(dataNode);
        }

        if (obj.TryGetPropertyValue("errors", out var errorsNode) && errorsNode is JsonArray errorsArray)
        {
            document.Errors = errorsArray.Select(e => ReadError((JsonObject)e!)).ToList();
        }

        if (obj.TryGetPropertyValue("meta", out var metaNode) && metaNode is JsonObject metaObj)
        {
            document.Meta = ReadMeta(metaObj);
        }

        if (obj.TryGetPropertyValue("links", out var linksNode) && linksNode is JsonObject linksObj)
        {
            document.Links = ReadLinks(linksObj);
        }

        return document;
    }

    private static JsonApiDocumentData ReadData(JsonNode? dataNode)
    {
        if (dataNode is JsonArray array)
        {
            return new JsonApiDocumentData
            {
                IsCollection = true,
                Collection = array.Select(n => ReadResource((JsonObject)n!)).ToList(),
            };
        }

        return new JsonApiDocumentData
        {
            IsCollection = false,
            Single = dataNode is JsonObject obj ? ReadResource(obj) : null,
        };
    }

    private static ResourceObject ReadResource(JsonObject obj)
    {
        var resource = new ResourceObject
        {
            Type = obj["type"]!.GetValue<string>(),
            Id = obj.TryGetPropertyValue("id", out var idNode) ? idNode?.GetValue<string>() : null,
        };

        if (obj.TryGetPropertyValue("attributes", out var attributesNode) && attributesNode is JsonObject attributesObj)
        {
            resource.Attributes = attributesObj.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        }

        if (obj.TryGetPropertyValue("relationships", out var relationshipsNode) && relationshipsNode is JsonObject relationshipsObj)
        {
            resource.Relationships = relationshipsObj.ToDictionary(kv => kv.Key, kv => ReadRelationship((JsonObject)kv.Value!));
        }

        if (obj.TryGetPropertyValue("links", out var linksNode) && linksNode is JsonObject linksObj)
        {
            resource.Links = ReadLinks(linksObj);
        }

        if (obj.TryGetPropertyValue("meta", out var metaNode) && metaNode is JsonObject metaObj)
        {
            resource.Meta = ReadMeta(metaObj);
        }

        return resource;
    }

    private static RelationshipObject ReadRelationship(JsonObject obj)
    {
        var relationship = new RelationshipObject();

        if (obj.TryGetPropertyValue("data", out var dataNode))
        {
            if (dataNode is JsonArray array)
            {
                relationship.IsToMany = true;
                relationship.ManyData = array.Select(n => ReadIdentifier((JsonObject)n!)).ToList();
            }
            else
            {
                relationship.IsToMany = false;
                relationship.SingleData = dataNode is JsonObject identifierObj ? ReadIdentifier(identifierObj) : null;
            }
        }

        if (obj.TryGetPropertyValue("links", out var linksNode) && linksNode is JsonObject linksObj)
        {
            relationship.Links = ReadLinks(linksObj);
        }

        if (obj.TryGetPropertyValue("meta", out var metaNode) && metaNode is JsonObject metaObj)
        {
            relationship.Meta = ReadMeta(metaObj);
        }

        return relationship;
    }

    private static ResourceIdentifierObject ReadIdentifier(JsonObject obj) => new()
    {
        Type = obj["type"]!.GetValue<string>(),
        Id = obj["id"]!.GetValue<string>(),
    };

    private static ErrorObject ReadError(JsonObject obj)
    {
        var error = new ErrorObject
        {
            Id = obj.TryGetPropertyValue("id", out var id) ? id?.GetValue<string>() : null,
            Status = obj.TryGetPropertyValue("status", out var status) ? status?.GetValue<string>() : null,
            Code = obj.TryGetPropertyValue("code", out var code) ? code?.GetValue<string>() : null,
            Title = obj.TryGetPropertyValue("title", out var title) ? title?.GetValue<string>() : null,
            Detail = obj.TryGetPropertyValue("detail", out var detail) ? detail?.GetValue<string>() : null,
        };

        if (obj.TryGetPropertyValue("source", out var sourceNode) && sourceNode is JsonObject sourceObj)
        {
            error.Source = new ErrorSourceObject
            {
                Pointer = sourceObj.TryGetPropertyValue("pointer", out var pointer) ? pointer?.GetValue<string>() : null,
                Parameter = sourceObj.TryGetPropertyValue("parameter", out var parameter) ? parameter?.GetValue<string>() : null,
            };
        }

        if (obj.TryGetPropertyValue("meta", out var metaNode) && metaNode is JsonObject metaObj)
        {
            error.Meta = ReadMeta(metaObj);
        }

        return error;
    }

    private static LinksObject ReadLinks(JsonObject obj)
    {
        var links = new LinksObject();
        foreach (var (key, value) in obj)
        {
            links[key] = value?.GetValue<string>();
        }

        return links;
    }

    private static MetaObject ReadMeta(JsonObject obj)
    {
        var meta = new MetaObject();
        foreach (var (key, value) in obj)
        {
            meta[key] = value;
        }

        return meta;
    }
}
