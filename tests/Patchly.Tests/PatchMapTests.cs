namespace Patchly.Tests;

public class Customer
{
    public string? GivenName { get; set; }
    public int Age { get; set; }
}

public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target)
    {
        if (patch.Provided.FirstName) target.GivenName = patch.FirstName;
        if (patch.Provided.Age) target.Age = patch.Age ?? 0;
    }
}

public class PatchMapTests
{
    [Fact]
    public void PatchMap_AppliesPatchToTarget()
    {
        var patch = System.Text.Json.JsonSerializer.Deserialize<CustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        var customer = new Customer { GivenName = "Bob", Age = 25 };
        var map = new CustomerPatchMap();
        map.Apply(patch!, customer);

        customer.GivenName.Should().Be("Alice");
        customer.Age.Should().Be(30);
    }

    [Fact]
    public void PatchMap_OnlyAppliesProvidedProperties()
    {
        var patch = System.Text.Json.JsonSerializer.Deserialize<CustomerPatch>(
            """{"firstName":"Alice"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        var customer = new Customer { GivenName = "Bob", Age = 25 };
        var map = new CustomerPatchMap();
        map.Apply(patch!, customer);

        customer.GivenName.Should().Be("Alice");
        customer.Age.Should().Be(25);
    }
}
