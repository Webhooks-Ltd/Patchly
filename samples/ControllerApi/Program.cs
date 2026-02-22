using Patchly;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddPatchlyMaps();

var app = builder.Build();
app.MapControllers();
app.Run();
