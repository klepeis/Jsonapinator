// Demonstrates AddJsonApi()'s options.MapErrorsAlways() override: a JSON:API error document is
// produced for every invalid-ModelState/unhandled-exception response, regardless of what the
// client's Accept header asked for. Compare against Jsonapinator.Sample.ErrorHandling.Default,
// which is identical except for the AddJsonApi() call below.
//
// Try it (after `dotnet run`, default port 5292):
//
//   # Malformed body, NO JSON:API Accept header -> still a JSON:API errors document, 400
//   # (Jsonapinator.Sample.ErrorHandling.Default would return ProblemDetails here instead.)
//   curl -i -X POST http://localhost:5292/articles \
//     -H "Content-Type: application/vnd.api+json" -H "Accept: application/json" \
//     -d "not json"
//
//   # Unhandled exception, no JSON:API Accept header -> still a generic JSON:API error, 500
//   curl -i http://localhost:5292/articles/boom

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonApi(options => options.MapErrorsAlways());

var app = builder.Build();

// Required for JsonApiExceptionHandler to actually run -- AddJsonApi() only registers it as a
// service; this call wires it into the pipeline.
app.UseExceptionHandler();

app.MapControllers();

app.Run();
