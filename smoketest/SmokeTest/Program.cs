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

if (!patch!.Provided.Name || !patch.Provided.Age || patch.Provided.Email)
    throw new Exception("Tracking mismatch");

Console.WriteLine("--- Patchly Smoke Test ---");
Console.WriteLine();
Console.WriteLine($"Input: {json}");
Console.WriteLine();
Console.WriteLine("Field  | Sent? | Value");
Console.WriteLine("-------|-------|------");
Console.WriteLine($"Name   | {"yes",-5} | {patch.Name}");
Console.WriteLine($"Age    | {"yes",-5} | {patch.Age?.ToString() ?? "null"}");
Console.WriteLine($"Email  | {"no",-5} | -");
Console.WriteLine();

var services = new ServiceCollection();
services.AddPatchlyMaps();
var sp = services.BuildServiceProvider();
var applier = sp.GetRequiredService<IPatchApplier>();

var customer = new Customer { Name = "Bob", Age = 25 };
var before = $"{{Name={customer.Name}, Age={customer.Age}}}";
applier.Apply(patch, customer);
var after = $"{{Name={customer.Name}, Age={customer.Age?.ToString() ?? "null"}}}";

if (customer.Name != "Alice")
    throw new Exception($"PatchMap mismatch: expected Alice, got {customer.Name}");

Console.WriteLine($"Patch apply: {before} -> {after}");
Console.WriteLine();
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
    public int? Age { get; set; }
}

public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target)
    {
        if (patch.Provided.Name) target.Name = patch.Name;
        if (patch.Provided.Age) target.Age = patch.Age;
    }
}

[JsonSerializable(typeof(CustomerPatch))]
internal partial class SmokeTestJsonContext : JsonSerializerContext;
