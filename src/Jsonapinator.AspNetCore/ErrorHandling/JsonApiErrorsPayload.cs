using Jsonapinator.Document;

namespace Jsonapinator.AspNetCore.ErrorHandling;

/// <summary>
/// Marker payload wrapping <see cref="ErrorObject"/>s produced by
/// <see cref="JsonApiInvalidModelStateResponseFactory"/>, so
/// <see cref="Formatters.JsonApiOutputFormatter"/> can distinguish "serialize this as a JSON:API
/// errors document" (no "data" member) from "serialize this as a normal resource/collection
/// document" — deliberately not just <see cref="IEnumerable{ErrorObject}"/>, which could
/// ambiguously collide with an action that intentionally returns a list of <see cref="ErrorObject"/>
/// as ordinary resource data.
/// </summary>
internal sealed class JsonApiErrorsPayload
{
    public JsonApiErrorsPayload(IReadOnlyList<ErrorObject> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<ErrorObject> Errors { get; }
}
