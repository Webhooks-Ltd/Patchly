using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Patchly;

var options = new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        PatchlyJsonTypeInfoResolver.Default,
        SmokeTestJsonContext.Default)
};

var json = """{"Name":"Alice","Age":null}""";
var patch = JsonSerializer.Deserialize(json, (JsonTypeInfo<CustomerPatch>)options.GetTypeInfo(typeof(CustomerPatch)));

Console.WriteLine($"Name provided: {patch!.Provided.Name}");
Console.WriteLine($"Age provided: {patch.Provided.Age}");
Console.WriteLine($"Email provided: {patch.Provided.Email}");
Console.WriteLine($"Name value: {patch.Name}");
Console.WriteLine($"Age value: {patch.Age?.ToString() ?? "null"}");

if (!patch.Provided.Name || !patch.Provided.Age || patch.Provided.Email)
    throw new Exception("Tracking mismatch");

// Verify PatchMap + IPatchApplier via DI
var services = new ServiceCollection();
services.AddPatchlyMaps();
var sp = services.BuildServiceProvider();
var applier = sp.GetRequiredService<IPatchApplier>();

var customer = new Customer { Name = "Bob", Age = 25 };
applier.Apply(patch, customer);

if (customer.Name != "Alice")
    throw new Exception($"PatchMap mismatch: expected Alice, got {customer.Name}");

Console.WriteLine($"PatchMap applied: Name={customer.Name}, Age={customer.Age}");
Console.WriteLine("Smoke test passed!");

[PatchDocument]
public partial class CustomerPatch
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Email { get; set; }
}

public class Customer
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target)
    {
        if (patch.Provided.Name) target.Name = patch.Name;
        if (patch.Provided.Age) target.Age = patch.Age ?? 0;
    }
}

[JsonSerializable(typeof(CustomerPatch))]
internal partial class SmokeTestJsonContext : JsonSerializerContext;
