// Demonstrates AddJsonApi()'s DEFAULT error-model behavior: negotiation-aware. A JSON:API
// error document is only produced when the client's Accept header actually asked for
// application/vnd.api+json — otherwise ASP.NET Core's normal ProblemDetails response is
// preserved untouched. Compare against Jsonapinator.Sample.ErrorHandling.AlwaysMap, which is
// identical except for the AddJsonApi() call below.
//
// Try it (after `dotnet run`, default port 5291):
//
//   # Malformed body + Accept: application/vnd.api+json -> a JSON:API errors document, 400
//   curl -i -X POST http://localhost:5291/articles \
//     -H "Content-Type: application/vnd.api+json" -H "Accept: application/vnd.api+json" \
//     -d "not json"
//
//   # Same malformed body, no JSON:API Accept header -> ASP.NET Core's default ProblemDetails, 400
//   curl -i -X POST http://localhost:5291/articles \
//     -H "Content-Type: application/vnd.api+json" -H "Accept: application/json" \
//     -d "not json"
//
//   # Unhandled exception + Accept: application/vnd.api+json -> a generic JSON:API error, 500
//   curl -i http://localhost:5291/articles/boom -H "Accept: application/vnd.api+json"

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonApi(); // negotiation-aware error mapping is the default

var app = builder.Build();

// Required for JsonApiExceptionHandler to actually run -- AddJsonApi() only registers it as a
// service; this call wires it into the pipeline.
app.UseExceptionHandler();

app.MapControllers();

app.Run();
