using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Patchly.Tests;

public class AotSerializationTests
{
    private static JsonSerializerOptions CreateAotLikeOptions(JsonNamingPolicy? namingPolicy = null, bool caseInsensitive = false)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                PatchlyJsonTypeInfoResolver.Default,
                new DefaultJsonTypeInfoResolver()),
            PropertyNamingPolicy = namingPolicy,
            PropertyNameCaseInsensitive = caseInsensitive
        };
        return options;
    }

    [Fact]
    public void ResolverChain_DeserializesPartialJson()
    {
        var options = CreateAotLikeOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":30}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }

    [Fact]
    public void ResolverChain_DistinguishesNullFromAbsent()
    {
        var options = CreateAotLikeOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":null}""", options)!;

        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().BeNull();
    }

    [Fact]
    public void ResolverChain_ProvidedAccessorWorks()
    {
        var options = CreateAotLikeOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", options)!;

        patch.Provided.FirstName.Should().BeTrue();
        patch.Provided.LastName.Should().BeFalse();
        patch.Provided.Age.Should().BeFalse();
    }

    [Fact]
    public void ResolverChain_SerializationWorks()
    {
        var options = CreateAotLikeOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":30}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        json.Should().Contain("firstName");
        json.Should().NotContain("_providedProperties");
        json.Should().NotContain("providedProperties");
    }

    [Fact]
    public void ResolverChain_CamelCaseNamingPolicy()
    {
        var options = CreateAotLikeOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverChain_PascalCaseNamingPolicy()
    {
        var options = CreateAotLikeOptions();

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"FirstName":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverChain_CaseInsensitiveMatching()
    {
        var options = CreateAotLikeOptions(caseInsensitive: true);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"FIRSTNAME":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverChain_InsertAtZero()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.TypeInfoResolverChain.Insert(0, PatchlyJsonTypeInfoResolver.Default);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Bob"}""", options)!;

        patch.FirstName.Should().Be("Bob");
        patch.WasProvided("FirstName").Should().BeTrue();
    }
}
