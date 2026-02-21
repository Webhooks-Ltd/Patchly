using System.Text.Json;
using Patchly;

var json = """{"Name":"Alice","Age":null}""";
var patch = JsonSerializer.Deserialize<CustomerPatch>(json);

Console.WriteLine($"Name provided: {patch!.Provided.Name}");
Console.WriteLine($"Age provided: {patch.Provided.Age}");
Console.WriteLine($"Email provided: {patch.Provided.Email}");
Console.WriteLine($"Name value: {patch.Name}");
Console.WriteLine($"Age value: {patch.Age}");

if (!patch.Provided.Name || !patch.Provided.Age || patch.Provided.Email)
    throw new Exception("Tracking mismatch");

Console.WriteLine("Smoke test passed!");

[PatchDocument]
public partial class CustomerPatch
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Email { get; set; }
}
