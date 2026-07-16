using Jsonapinator.Attributes;
using Jsonapinator.Metadata;

namespace Jsonapinator.Tests.Metadata;

public class ResourceTypeResolverConcurrencyTests
{
    [JsonApiResource("widgets")]
    private sealed class Widget
    {
        [JsonApiId]
        public string Id { get; set; } = "";

        [JsonApiAttribute]
        public string Name { get; set; } = "";
    }

    [Fact]
    public async Task Resolve_is_safe_under_concurrent_first_time_resolution_of_the_same_type()
    {
        var resolver = new ResourceTypeResolver();

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => resolver.Resolve(typeof(Widget))))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.Equal("widgets", results[0].ResourceType);
    }
}
