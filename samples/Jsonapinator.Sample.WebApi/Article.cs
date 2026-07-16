namespace Jsonapinator.Sample.WebApi;

// No Jsonapinator.Attributes — mapped by convention (see Program.cs's AddJsonApi(o => o.UseConventions())).
// "Id" becomes the resource id; the JSON:API "type" member is the camelCase class name, "article".
public class Article
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
}
