using Jsonapinator.Attributes;

namespace Jsonapinator.Tests.Attributes;

public class AttributeTests
{
    [Fact]
    public void JsonApiResourceAttribute_stores_the_resource_type_name()
    {
        var attribute = new JsonApiResourceAttribute("articles");

        Assert.Equal("articles", attribute.ResourceType);
    }

    [Fact]
    public void JsonApiResourceAttribute_can_only_be_applied_once_per_class()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(JsonApiResourceAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void JsonApiIdAttribute_targets_properties_only()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(JsonApiIdAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Property, usage.ValidOn);
    }

    [Fact]
    public void JsonApiAttributeAttribute_targets_properties_only()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(JsonApiAttributeAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Property, usage.ValidOn);
    }

    [Fact]
    public void JsonApiRelationshipAttribute_stores_name_and_kind()
    {
        var attribute = new JsonApiRelationshipAttribute("author", RelationshipKind.ToOne);

        Assert.Equal("author", attribute.Name);
        Assert.Equal(RelationshipKind.ToOne, attribute.Kind);
    }

    [Fact]
    public void JsonApiRelationshipAttribute_supports_to_many_kind()
    {
        var attribute = new JsonApiRelationshipAttribute("comments", RelationshipKind.ToMany);

        Assert.Equal(RelationshipKind.ToMany, attribute.Kind);
    }
}
