namespace Jsonapinator.Attributes;

/// <summary>
/// Marks the property that supplies a resource's JSON:API resource-level "meta". The property's
/// CLR type must be exactly <see cref="Document.MetaObject"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class JsonApiMetaAttribute : Attribute
{
}
