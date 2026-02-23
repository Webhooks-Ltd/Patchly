using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Patchly.Tests;

public class ResolverDeserializationTests
{
    private static JsonSerializerOptions CreateResolverOptions(
        JsonNamingPolicy? namingPolicy = null,
        bool caseInsensitive = false,
        JsonIgnoreCondition? ignoreCondition = null)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                PatchlyJsonTypeInfoResolver.Default,
                new DefaultJsonTypeInfoResolver()),
            PropertyNamingPolicy = namingPolicy,
            PropertyNameCaseInsensitive = caseInsensitive
        };

        if (ignoreCondition.HasValue)
            options.DefaultIgnoreCondition = ignoreCondition.Value;

        return options;
    }

    [Fact]
    public void ResolverPath_NullVsAbsentDistinction()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":null}""", options)!;

        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue("explicitly null should be tracked as provided");
        patch.WasProvided("LastName").Should().BeFalse("absent property should not be tracked");
        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().BeNull();
    }

    [Fact]
    public void ResolverPath_ProvidedAccessorWorks()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", options)!;

        patch.Provided.FirstName.Should().BeTrue();
        patch.Provided.LastName.Should().BeFalse();
        patch.Provided.Age.Should().BeFalse();
    }

    [Fact]
    public void ResolverPath_MatchesConverterPath()
    {
        var resolverOptions = CreateResolverOptions(JsonNamingPolicy.CamelCase);
        var converterOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = """{"firstName":"Alice","age":30}""";
        var resolverPatch = JsonSerializer.Deserialize<CustomerPatch>(json, resolverOptions)!;
        var converterPatch = JsonSerializer.Deserialize<CustomerPatch>(json, converterOptions)!;

        resolverPatch.FirstName.Should().Be(converterPatch.FirstName);
        resolverPatch.LastName.Should().Be(converterPatch.LastName);
        resolverPatch.Age.Should().Be(converterPatch.Age);
        resolverPatch.WasProvided("FirstName").Should().Be(converterPatch.WasProvided("FirstName"));
        resolverPatch.WasProvided("LastName").Should().Be(converterPatch.WasProvided("LastName"));
        resolverPatch.WasProvided("Age").Should().Be(converterPatch.WasProvided("Age"));
    }

    [Fact]
    public void ResolverPath_SerializationExcludesTrackingInfrastructure()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        json.Should().Contain("firstName");
        json.Should().NotContain("_providedProperties");
        json.Should().NotContain("providedProperties");
        json.Should().NotContain("wasProvided");
        json.Should().NotContain("\"provided\"");
    }

    [Fact]
    public void ResolverPath_JsonPropertyNameOverrideRespected()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<JsonPropertyNamePatch>("""{"first_name":"Alice","lastName":"Smith"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.LastName.Should().Be("Smith");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeTrue();
    }

    [Fact]
    public void ResolverPath_NamingPolicyCamelCase()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverPath_NamingPolicySnakeCase()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.SnakeCaseLower);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"first_name":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverPath_WhenWritingNull_RespectedInSerialization()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase, ignoreCondition: JsonIgnoreCondition.WhenWritingNull);

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","lastName":null}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        json.Should().Contain("firstName");
        json.Should().NotContain("lastName");
    }

    [Fact]
    public void ResolverPath_JsonIgnorePropertiesExcluded()
    {
        var options = CreateResolverOptions(JsonNamingPolicy.CamelCase);

        var patch = JsonSerializer.Deserialize<JsonIgnorePatch>("""{"firstName":"Alice"}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("InternalNote").Should().BeFalse();
        json.Should().NotContain("internalNote");
    }

    [Fact]
    public void ConverterFallback_DeserializationWorksWithoutResolver()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":30}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }
}
