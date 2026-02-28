using System.Text.Json;
using System.Text.Json.Serialization;

namespace Patchly.Tests;

public class SerializationTests
{
    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions PascalCase = new();
    private static readonly JsonSerializerOptions SnakeCaseLower = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void PropertyPresentWithValue_WasProvidedTrue()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.Provided.FirstName.Should().BeTrue();
    }

    [Fact]
    public void PropertyPresentWithNull_WasProvidedTrue()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":null}""", CamelCase)!;

        patch.FirstName.Should().BeNull();
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.Provided.FirstName.Should().BeTrue();
    }

    [Fact]
    public void PropertyAbsent_WasProvidedFalse()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.LastName.Should().BeNull();
        patch.WasProvided("LastName").Should().BeFalse();
        patch.Provided.LastName.Should().BeFalse();
    }

    [Fact]
    public void EmptyJsonObject_AllFalse()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{}""", CamelCase)!;

        patch.WasProvided("FirstName").Should().BeFalse();
        patch.WasProvided("LastName").Should().BeFalse();
        patch.WasProvided("Age").Should().BeFalse();
        patch.Provided.FirstName.Should().BeFalse();
        patch.Provided.LastName.Should().BeFalse();
        patch.Provided.Age.Should().BeFalse();
        patch.ProvidedProperties.Should().BeEmpty();
    }

    [Fact]
    public void NullJsonToken_ReturnsNull()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("null", CamelCase);

        patch.Should().BeNull();
    }

    [Fact]
    public void UnknownProperties_SilentlyIgnored()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","unknownProp":"value"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void DuplicateProperties_UsesLastValue()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","firstName":"Bob"}""", CamelCase)!;

        patch.FirstName.Should().Be("Bob");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void WasProvided_CaseInsensitiveForCSharpNames()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.WasProvided("firstname").Should().BeTrue();
        patch.WasProvided("FIRSTNAME").Should().BeTrue();
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void WasProvided_UnknownPropertyName_ReturnsFalse()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.WasProvided("NonExistent").Should().BeFalse();
    }

    [Fact]
    public void DeterministicState_OmittedNullValue()
    {
        var omitted = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{}""", CamelCase)!;
        var withNull = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"firstName":null}""", CamelCase)!;
        var withValue = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"firstName":"Alice"}""", CamelCase)!;

        omitted.State.FirstName.Should().Be(PatchValueState.Omitted);
        withNull.State.FirstName.Should().Be(PatchValueState.Null);
        withValue.State.FirstName.Should().Be(PatchValueState.Value);
    }

    [Fact]
    public void DeterministicGetState_CaseInsensitiveAndUnknown()
    {
        var patch = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.GetState("firstname").Should().Be(PatchValueState.Value);
        patch.GetState("FIRSTNAME").Should().Be(PatchValueState.Value);
        patch.GetState("FirstName").Should().Be(PatchValueState.Value);
        patch.GetState("DoesNotExist").Should().Be(PatchValueState.Omitted);
    }

    [Fact]
    public void DeterministicNestedPatchDocument_TracksIndependently()
    {
        var patch = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"address":{"city":"Seattle"}}""", CamelCase)!;

        patch.State.Address.Should().Be(PatchValueState.Value);
        patch.Address.Should().NotBeNull();
        patch.Address!.State.City.Should().Be(PatchValueState.Value);
        patch.Address.State.Line1.Should().Be(PatchValueState.Omitted);
    }

    [Fact]
    public void DeterministicNestedPatchDocument_NullAndOmitted()
    {
        var omitted = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{}""", CamelCase)!;
        var withNull = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"address":null}""", CamelCase)!;

        omitted.State.Address.Should().Be(PatchValueState.Omitted);
        withNull.State.Address.Should().Be(PatchValueState.Null);
    }

    [Fact]
    public void DeterministicCollectionState_OmittedNullEmptyAndNonEmpty()
    {
        var omitted = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{}""", CamelCase)!;
        var withNull = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"tags":null}""", CamelCase)!;
        var withEmpty = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"tags":[]}""", CamelCase)!;
        var withValues = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"tags":["vip","priority"]}""", CamelCase)!;

        omitted.State.Tags.Should().Be(PatchValueState.Omitted);
        withNull.State.Tags.Should().Be(PatchValueState.Null);
        withEmpty.State.Tags.Should().Be(PatchValueState.Value);
        withEmpty.Tags.Should().NotBeNull().And.BeEmpty();
        withValues.State.Tags.Should().Be(PatchValueState.Value);
        withValues.Tags.Should().Equal("vip", "priority");
    }

    [Fact]
    public void DeterministicDuplicateProperty_UsesLastValueForState()
    {
        var patch = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"firstName":"Alice","firstName":null}""", CamelCase)!;

        patch.FirstName.Should().BeNull();
        patch.State.FirstName.Should().Be(PatchValueState.Null);
    }

    [Fact]
    public void FreshInstance_EmptyTracking()
    {
        var patch = new CustomerPatch();

        patch.WasProvided("FirstName").Should().BeFalse();
        patch.ProvidedProperties.Should().BeEmpty();
        patch.Provided.FirstName.Should().BeFalse();
    }

    [Fact]
    public void AllSupportedPropertyTypes()
    {
        var json = """
            {
                "stringProp": "hello",
                "intProp": 42,
                "boolProp": true,
                "dateTimeProp": "2024-01-15T10:30:00",
                "dateTimeOffsetProp": "2024-01-15T10:30:00+05:00",
                "guidProp": "12345678-1234-1234-1234-123456789012",
                "enumProp": 1,
                "decimalProp": 123.45,
                "doubleProp": 3.14,
                "nestedProp": {"name": "nested"},
                "listProp": ["a", "b"],
                "arrayProp": ["c", "d"],
                "dictionaryProp": {"key1": 1, "key2": 2}
            }
            """;

        var patch = JsonSerializer.Deserialize<AllTypesPatch>(json, CamelCase)!;

        patch.StringProp.Should().Be("hello");
        patch.IntProp.Should().Be(42);
        patch.BoolProp.Should().BeTrue();
        patch.DateTimeProp.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0));
        patch.GuidProp.Should().Be(Guid.Parse("12345678-1234-1234-1234-123456789012"));
        patch.EnumProp.Should().Be(TestEnum.ValueA);
        patch.DecimalProp.Should().Be(123.45m);
        patch.DoubleProp.Should().Be(3.14);
        patch.NestedProp!.Name.Should().Be("nested");
        patch.ListProp.Should().BeEquivalentTo(new[] { "a", "b" });
        patch.ArrayProp.Should().BeEquivalentTo(new[] { "c", "d" });
        patch.DictionaryProp.Should().ContainKey("key1").WhoseValue.Should().Be(1);

        patch.WasProvided("StringProp").Should().BeTrue();
        patch.WasProvided("IntProp").Should().BeTrue();
        patch.WasProvided("BoolProp").Should().BeTrue();
        patch.WasProvided("DateTimeProp").Should().BeTrue();
        patch.WasProvided("DateTimeOffsetProp").Should().BeTrue();
        patch.WasProvided("GuidProp").Should().BeTrue();
        patch.WasProvided("EnumProp").Should().BeTrue();
        patch.WasProvided("DecimalProp").Should().BeTrue();
        patch.WasProvided("DoubleProp").Should().BeTrue();
        patch.WasProvided("NestedProp").Should().BeTrue();
        patch.WasProvided("ListProp").Should().BeTrue();
        patch.WasProvided("ArrayProp").Should().BeTrue();
        patch.WasProvided("DictionaryProp").Should().BeTrue();
    }

    [Fact]
    public void JsonPropertyNameOverride()
    {
        var patch = JsonSerializer.Deserialize<JsonPropertyNamePatch>("""{"first_name":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.Provided.FirstName.Should().BeTrue();
    }

    [Fact]
    public void CamelCaseNamingPolicy()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice"}""", CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void CaseInsensitivePropertyMatching()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"FIRSTNAME":"Alice"}""", options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void JsonSerializerDefaultsWeb_NumberFromString()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"age":"42"}""", options)!;

        patch.Age.Should().Be(42);
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void DefaultJsonSerializerOptions_PascalCase()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"FirstName":"Alice"}""", PascalCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void SnakeCaseLowerNamingPolicy()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"first_name":"Alice"}""", SnakeCaseLower)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void SerializationOutput_ExcludesTrackingFields()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","age":30}""", CamelCase)!;
        var json = JsonSerializer.Serialize(patch, CamelCase);

        json.Should().NotContain("_providedProperties");
        json.Should().NotContain("provided");
        json.Should().NotContain("wasProvided");
        json.Should().NotContain("providedProperties");
    }

    [Fact]
    public void Serialization_RespectsDefaultIgnoreCondition_WhenWritingNull()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","lastName":null}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        json.Should().Contain("firstName");
        json.Should().NotContain("lastName");
    }

    [Fact]
    public void TypeMismatch_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<CustomerPatch>("""{"Age":"not-a-number"}""", PascalCase);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void MalformedJson_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<CustomerPatch>("""{invalid""", CamelCase);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void JsonArrayInsteadOfObject_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<CustomerPatch>("""[1,2,3]""", CamelCase);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void NestedPatchDocument_TracksIndependently()
    {
        var json = """{"firstName":"Alice","address":{"line1":"123 Main St"}}""";
        var patch = JsonSerializer.Deserialize<NestedPatchDocument>(json, CamelCase)!;

        patch.Provided.FirstName.Should().BeTrue();
        patch.Provided.Address.Should().BeTrue();
        patch.Address!.Provided.Line1.Should().BeTrue();
        patch.Address.Provided.City.Should().BeFalse();
    }

    [Fact]
    public void JsonIgnore_ExcludedFromTracking()
    {
        var json = """{"firstName":"Alice","internalNote":"secret"}""";
        var patch = JsonSerializer.Deserialize<JsonIgnorePatch>(json, CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("InternalNote").Should().BeFalse();
        patch.InternalNote.Should().BeNull();
    }

    [Fact]
    public void JsonInclude_NonPublicProperty_IncludedInTracking()
    {
        var json = """{"publicName":"Alice","secretCode":"abc123"}""";
        var patch = JsonSerializer.Deserialize<JsonIncludePatch>(json, CamelCase)!;

        patch.PublicName.Should().Be("Alice");
        patch.WasProvided("PublicName").Should().BeTrue();
        patch.Provided.PublicName.Should().BeTrue();
        patch.SecretCode.Should().Be("abc123");
        patch.WasProvided("SecretCode").Should().BeTrue();
        patch.Provided.SecretCode.Should().BeTrue();
    }

    [Fact]
    public void JsonNumberHandling_PerProperty_AllowReadingFromString()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = """{"count":"42","name":"test"}""";
        var patch = JsonSerializer.Deserialize<JsonNumberHandlingPatch>(json, options)!;

        patch.Count.Should().Be(42);
        patch.WasProvided("Count").Should().BeTrue();
        patch.Name.Should().Be("test");
    }

    [Fact]
    public void Serialization_RespectsDefaultIgnoreCondition_WhenWritingDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        var patch = JsonSerializer.Deserialize<CustomerPatch>("""{"firstName":"Alice","lastName":null,"age":null}""", options)!;
        var json = JsonSerializer.Serialize(patch, options);

        json.Should().Contain("firstName");
        json.Should().NotContain("lastName");
        json.Should().NotContain("age");
    }

    [Fact]
    public void DeterministicSerialization_ExcludesStateInfrastructure()
    {
        var patch = JsonSerializer.Deserialize<DeterministicPatchDocument>("""{"firstName":"Alice"}""", CamelCase)!;
        var json = JsonSerializer.Serialize(patch, CamelCase);

        json.Should().NotContain("state");
        json.Should().NotContain("provided");
        json.Should().Contain("firstName");
    }

    [Fact]
    public void RequiredProperties_CompileAndTrackCorrectly()
    {
        var patch = JsonSerializer.Deserialize<RequiredPatch>("""{"FirstName":"Alice"}""", PascalCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.Provided.FirstName.Should().BeTrue();
        patch.WasProvided("LastName").Should().BeFalse();
    }
}
