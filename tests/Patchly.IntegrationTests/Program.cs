using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Patchly.IntegrationTests;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddApplicationPart(typeof(CustomersController).Assembly);
builder.Services.AddPatchly();
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapControllers();

app.MapPatch("/minimal/customers/{id}", (string id, [FromBody] UpdateCustomerPatch patch) =>
{
    var result = new Dictionary<string, object?>
    {
        ["id"] = id,
        ["providedFirstName"] = patch.Provided.FirstName,
        ["providedLastName"] = patch.Provided.LastName,
        ["providedAge"] = patch.Provided.Age,
        ["firstName"] = patch.FirstName,
        ["lastName"] = patch.LastName,
        ["age"] = patch.Age
    };
    return Results.Ok(result);
});

app.MapPatch("/minimal/buffered/{id}", (string id, [FromBody] BufferedPatch patch) =>
{
    return Results.Ok(new { id, name = patch.Name, value = patch.Value });
});

app.MapOpenApi();
app.Run();

public partial class Program;
