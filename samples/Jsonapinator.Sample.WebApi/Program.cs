var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonApi(); // convention-based mapping is the default

var app = builder.Build();

app.MapControllers();

app.Run();
