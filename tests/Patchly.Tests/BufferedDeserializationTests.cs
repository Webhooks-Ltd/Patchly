using System.Text.Json;
using System.Text.Json.Serialization;

namespace Patchly.Tests;

public class BufferedDeserializationTests
{
    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);

    [Fact]
    public void InitOnly_BasicDeserialization()
    {
        var patch = JsonSerializer.Deserialize<InitOnlyPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.LastName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }

    [Fact]
    public void InitOnly_MixedWithSet()
    {
        var patch = JsonSerializer.Deserialize<MixedInitSetPatch>("""{"name":"Alice","age":30}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("Name").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void InitOnly_NullValue_TrackedAsProvided()
    {
        var patch = JsonSerializer.Deserialize<InitOnlyPatch>("""{"firstName":null}""", CamelCase)!;

        patch.FirstName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void InitOnly_Required()
    {
        var patch = JsonSerializer.Deserialize<RequiredInitPatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
    }

    [Fact]
    public void InitOnly_ProvidedAccessor()
    {
        var patch = JsonSerializer.Deserialize<InitOnlyPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.Provided.FirstName.Should().BeTrue();
        patch.Provided.LastName.Should().BeFalse();
    }

    [Fact]
    public void JsonConstructor_BasicDeserialization()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorBasicPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.LastName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }

    [Fact]
    public void JsonConstructor_WithInitProperties()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorWithInitPatch>("""{"name":"Alice","age":30}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("Name").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void JsonConstructor_PropertiesNotCoveredByConstructor()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorMixedPatch>("""{"name":"Alice","age":30}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("Name").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void JsonConstructor_EmptyJson()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorBasicPatch>("""{}""", CamelCase)!;

        patch.FirstName.Should().BeNull();
        patch.LastName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeFalse();
        patch.WasProvided("LastName").Should().BeFalse();
        patch.ProvidedProperties.Should().BeEmpty();
    }

    [Fact]
    public void JsonConstructor_NullVsAbsent()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorBasicPatch>("""{"firstName":null}""", CamelCase)!;

        patch.FirstName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }

    [Fact]
    public void JsonConstructor_DefaultValue()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorDefaultValuePatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
        patch.Role.Should().Be("user");
        patch.WasProvided("Role").Should().BeFalse();
    }

    [Fact]
    public void JsonConstructor_UnmatchedParamNoDefault_ReceivesLanguageDefault()
    {
        var patch = JsonSerializer.Deserialize<JsonConstructorUnmatchedNoDefaultPatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
    }

    [Fact]
    public void StreamingPath_StillUsedForSetOnlyProperties()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void JsonIgnore_InitOnlyProperty_ExcludedFromTracking()
    {
        var patch = JsonSerializer.Deserialize<JsonIgnoreInitPatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
        patch.WasProvided("Internal").Should().BeFalse();
    }

    [Fact]
    public void PrivateInit_WorksFromNestedConverter()
    {
        var patch = JsonSerializer.Deserialize<PrivateInitPatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
    }

    [Fact]
    public void RequiredInit_ObjectInitializerPath()
    {
        var patch = JsonSerializer.Deserialize<RequiredInitPatch>("""{"name":"Alice","other":"B"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.Other.Should().Be("B");
        patch.WasProvided("Name").Should().BeTrue();
        patch.WasProvided("Other").Should().BeTrue();
    }

    [Fact]
    public void RequiredInit_WithJsonConstructorAndSetsRequiredMembers()
    {
        var patch = JsonSerializer.Deserialize<RequiredInitWithSetsRequiredPatch>("""{"name":"Alice"}""", CamelCase)!;

        patch.Name.Should().Be("Alice");
        patch.WasProvided("Name").Should().BeTrue();
    }
}
