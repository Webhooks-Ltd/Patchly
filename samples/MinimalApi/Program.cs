using Patchly;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var customers = new Dictionary<int, Customer>
{
    [1] = new() { Name = "Alice", Email = "alice@example.com", Age = 30 },
    [2] = new() { Name = "Bob", Email = "bob@example.com", Age = 25 },
};

app.MapGet("/customers/{id}", (int id) =>
    customers.TryGetValue(id, out var customer) ? Results.Ok(customer) : Results.NotFound());

app.MapPatch("/customers/{id}", (int id, CustomerPatch patch) =>
{
    if (!customers.TryGetValue(id, out var customer))
        return Results.NotFound();

    if (patch.Provided.Name)
        customer.Name = patch.Name;

    if (patch.Provided.Email)
        customer.Email = patch.Email;

    if (patch.Provided.Age)
        customer.Age = patch.Age;

    return Results.Ok(customer);
});

app.Run();

[PatchDocument]
public partial class CustomerPatch
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}

public class Customer
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}
