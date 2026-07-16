var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonApi(); // convention-based mapping is the default

var app = builder.Build();

// Required for unhandled exceptions to be mapped to JSON:API error documents by
// JsonApiExceptionHandler — AddJsonApi() registers the handler, but only app.UseExceptionHandler()
// actually wires it into the pipeline.
app.UseExceptionHandler();

app.MapControllers();

app.Run();
