namespace Jsonapinator.AspNetCore;

/// <summary>
/// Configures the <see cref="JsonApiSerializer"/> that backs the formatters registered by
/// <c>AddJsonApi()</c>. Defaults to convention-based mapping (no <c>Jsonapinator.Attributes</c>
/// required) — call <see cref="UseAttributes"/> to opt into attribute-based mapping instead.
/// </summary>
public sealed class JsonApiFormatterOptions
{
    internal bool ConventionMapping { get; private set; } = true;

    /// <summary>
    /// Map POCOs by convention (no <c>Jsonapinator.Attributes</c> required) — this is the
    /// default; calling this explicitly is only useful to undo a prior <see cref="UseAttributes"/>
    /// call in the same configuration delegate. See
    /// <see cref="Jsonapinator.Metadata.ConventionResourceTypeResolver"/>.
    /// </summary>
    public JsonApiFormatterOptions UseConventions()
    {
        ConventionMapping = true;
        return this;
    }

    /// <summary>
    /// Map POCOs via explicit <c>Jsonapinator.Attributes</c> (<c>[JsonApiResource]</c>,
    /// <c>[JsonApiId]</c>, etc.) instead of the default convention-based mapping. See
    /// <see cref="Jsonapinator.Metadata.ResourceTypeResolver"/>.
    /// </summary>
    public JsonApiFormatterOptions UseAttributes()
    {
        ConventionMapping = false;
        return this;
    }
}
