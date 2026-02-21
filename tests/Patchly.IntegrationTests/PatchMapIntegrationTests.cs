using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Patchly.IntegrationTests;

public class MappingCustomer
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
}

public class UpdateCustomerPatchMap : PatchMap<UpdateCustomerPatch, MappingCustomer>
{
    public override void Apply(UpdateCustomerPatch patch, MappingCustomer target)
    {
        if (patch.Provided.FirstName) target.FirstName = patch.FirstName;
        if (patch.Provided.LastName) target.LastName = patch.LastName;
        if (patch.Provided.Age) target.Age = patch.Age ?? 0;
    }
}

public interface IScopedDependency
{
    string Id { get; }
}

public class ScopedDependency : IScopedDependency
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

[PatchDocument]
public partial class WidgetPatch
{
    public string? Name { get; set; }
}

public class Widget
{
    public string? Name { get; set; }
    public string? ScopedId { get; set; }
}

public class WidgetPatchMap : PatchMap<WidgetPatch, Widget>
{
    private readonly IScopedDependency _dep;

    public WidgetPatchMap(IScopedDependency dep) => _dep = dep;

    public override void Apply(WidgetPatch patch, Widget target)
    {
        if (patch.Provided.Name) target.Name = patch.Name;
        target.ScopedId = _dep.Id;
    }
}

public class PatchMapIntegrationTests
{
    [Fact]
    public void AddPatchlyMaps_Resolve_IPatchApplier_ApplyPatch_VerifyMutation()
    {
        var services = new ServiceCollection();
        services.AddPatchlyMaps();
        var sp = services.BuildServiceProvider();

        var applier = sp.GetRequiredService<IPatchApplier>();

        var patch = JsonSerializer.Deserialize<UpdateCustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var customer = new MappingCustomer { FirstName = "Bob", LastName = "Jones", Age = 25 };
        applier.Apply(patch!, customer);

        customer.FirstName.Should().Be("Alice");
        customer.Age.Should().Be(30);
        customer.LastName.Should().Be("Jones");
    }

    [Fact]
    public void MapWithScopedDependency_ResolvesCorrectlyWithinScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddPatchlyMaps();
        var sp = services.BuildServiceProvider();

        var patch = JsonSerializer.Deserialize<WidgetPatch>(
            """{"name":"Gizmo"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        string scopedId1;
        string scopedId2;

        using (var scope1 = sp.CreateScope())
        {
            var applier = scope1.ServiceProvider.GetRequiredService<IPatchApplier>();
            var widget = new Widget();
            applier.Apply(patch!, widget);
            widget.Name.Should().Be("Gizmo");
            widget.ScopedId.Should().NotBeNullOrEmpty();
            scopedId1 = widget.ScopedId!;
        }

        using (var scope2 = sp.CreateScope())
        {
            var applier = scope2.ServiceProvider.GetRequiredService<IPatchApplier>();
            var widget = new Widget();
            applier.Apply(patch!, widget);
            scopedId2 = widget.ScopedId!;
        }

        scopedId1.Should().NotBe(scopedId2, "different scopes should use different scoped dependency instances");
    }

    [Fact]
    public void UnregisteredPair_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddPatchlyMaps();
        var sp = services.BuildServiceProvider();

        var applier = sp.GetRequiredService<IPatchApplier>();

        var patch = JsonSerializer.Deserialize<UpdateCustomerPatch>(
            """{"firstName":"Alice"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var act = () => applier.Apply(patch!, "not a valid target");

        act.Should().Throw<InvalidOperationException>()
            .And.Message.Should().Contain("UpdateCustomerPatch")
            .And.Contain("String");
    }

    [Fact]
    public void AddPatchlyMaps_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var act = () =>
        {
            services.AddPatchlyMaps();
            services.AddPatchlyMaps();
        };

        act.Should().NotThrow();

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IPatchApplier>().Should().NotBeNull();
    }
}
