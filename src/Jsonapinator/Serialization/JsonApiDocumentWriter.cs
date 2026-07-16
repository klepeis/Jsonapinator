using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonapinator.Document;

namespace Jsonapinator.Serialization;

/// <summary>
/// Default <see cref="IJsonApiWriter"/> — builds a <see cref="JsonNode"/> tree from a
/// <see cref="JsonApiDocument"/> and serializes it via <see cref="System.Text.Json"/>.
/// </summary>
public sealed class JsonApiDocumentWriter : IJsonApiWriter
{
    public string Write(JsonApiDocument document)
    {
        var root = new JsonObject();

        if (document.Data is not null)
        {
            root["data"] = WriteData(document.Data);
        }

        if (document.Errors is not null)
        {
            var errors = new JsonArray();
            foreach (var error in document.Errors)
            {
                errors.Add(WriteError(error));
            }

            root["errors"] = errors;
        }

        if (document.Meta is not null)
        {
            root["meta"] = WriteMeta(document.Meta);
        }

        if (document.Links is not null)
        {
            root["links"] = WriteLinks(document.Links);
        }

        return root.ToJsonString();
    }

    private static JsonNode? WriteData(JsonApiDocumentData data)
    {
        if (data.IsCollection)
        {
            var array = new JsonArray();
            foreach (var resource in data.Collection ?? Enumerable.Empty<ResourceObject>())
            {
                array.Add(WriteResource(resource));
            }

            return array;
        }

        return data.Single is null ? null : WriteResource(data.Single);
    }

    private static JsonObject WriteResource(ResourceObject resource)
    {
        var json = new JsonObject { ["type"] = resource.Type };

        if (resource.Id is not null)
        {
            json["id"] = resource.Id;
        }

        if (resource.Attributes is { Count: > 0 })
        {
            var attributes = new JsonObject();
            foreach (var (key, value) in resource.Attributes)
            {
                attributes[key] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType());
            }

            json["attributes"] = attributes;
        }

        if (resource.Relationships is { Count: > 0 })
        {
            var relationships = new JsonObject();
            foreach (var (key, value) in resource.Relationships)
            {
                relationships[key] = WriteRelationship(value);
            }

            json["relationships"] = relationships;
        }

        if (resource.Links is not null)
        {
            json["links"] = WriteLinks(resource.Links);
        }

        if (resource.Meta is not null)
        {
            json["meta"] = WriteMeta(resource.Meta);
        }

        return json;
    }

    private static JsonObject WriteRelationship(RelationshipObject relationship)
    {
        var json = new JsonObject();

        if (relationship.IsToMany)
        {
            var array = new JsonArray();
            foreach (var identifier in relationship.ManyData ?? new List<ResourceIdentifierObject>())
            {
                array.Add(WriteIdentifier(identifier));
            }

            json["data"] = array;
        }
        else
        {
            json["data"] = relationship.SingleData is null ? null : WriteIdentifier(relationship.SingleData);
        }

        if (relationship.Links is not null)
        {
            json["links"] = WriteLinks(relationship.Links);
        }

        if (relationship.Meta is not null)
        {
            json["meta"] = WriteMeta(relationship.Meta);
        }

        return json;
    }

    private static JsonObject WriteIdentifier(ResourceIdentifierObject identifier)
    {
        var json = new JsonObject { ["type"] = identifier.Type, ["id"] = identifier.Id };

        if (identifier.Meta is not null)
        {
            json["meta"] = WriteMeta(identifier.Meta);
        }

        return json;
    }

    private static JsonObject WriteError(ErrorObject error)
    {
        var json = new JsonObject();

        if (error.Id is not null) json["id"] = error.Id;
        if (error.Status is not null) json["status"] = error.Status;
        if (error.Code is not null) json["code"] = error.Code;
        if (error.Title is not null) json["title"] = error.Title;
        if (error.Detail is not null) json["detail"] = error.Detail;

        if (error.Source is not null)
        {
            var source = new JsonObject();
            if (error.Source.Pointer is not null) source["pointer"] = error.Source.Pointer;
            if (error.Source.Parameter is not null) source["parameter"] = error.Source.Parameter;
            json["source"] = source;
        }

        if (error.Meta is not null)
        {
            json["meta"] = WriteMeta(error.Meta);
        }

        return json;
    }

    private static JsonObject WriteLinks(LinksObject links)
    {
        var json = new JsonObject();
        foreach (var (key, value) in links)
        {
            json[key] = value;
        }

        return json;
    }

    private static JsonObject WriteMeta(MetaObject meta)
    {
        var json = new JsonObject();
        foreach (var (key, value) in meta)
        {
            json[key] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType());
        }

        return json;
    }
}
