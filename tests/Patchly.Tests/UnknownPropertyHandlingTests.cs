using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Patchly.Tests;

public class UnknownPropertyHandlingTests
{
    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);

    private static JsonSerializerOptions CreateResolverOptions(JsonUnmappedMemberHandling? unmappedMemberHandling = null)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                PatchlyJsonTypeInfoResolver.Default,
                new DefaultJsonTypeInfoResolver()),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (unmappedMemberHandling.HasValue)
            options.UnmappedMemberHandling = unmappedMemberHandling.Value;

        return options;
    }

    [Fact]
    public void ConverterPath_RejectMode_ThrowsForUnknownTopLevelProperty()
    {
        var act = () => JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .WithMessage("Unknown JSON properties on StrictCustomerPatch: 'unknownProp'");
    }

    [Fact]
    public void ConverterPath_RejectMode_ListsMultipleUnknownProperties()
    {
        var act = () => JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","foo":"x","bar":"y"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .Where(ex => ex.Message.Contains("'foo'") && ex.Message.Contains("'bar'"));
    }

    [Fact]
    public void ConverterPath_IgnoreMode_DefaultAttributeStillIgnoresUnknownProperties()
    {
        var patch = JsonSerializer.Deserialize<CustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ConverterPath_RejectMode_AllKnownPropertiesSucceed()
    {
        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            CamelCase)!;

        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void ConverterPath_RejectMode_EmptyObjectSucceeds()
    {
        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>("""{}""", CamelCase)!;

        patch.WasProvided("FirstName").Should().BeFalse();
        patch.WasProvided("LastName").Should().BeFalse();
        patch.WasProvided("Age").Should().BeFalse();
    }

    [Fact]
    public void ConverterPath_RejectMode_NullTokenReturnsNull()
    {
        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>("null", CamelCase);

        patch.Should().BeNull();
    }

    [Fact]
    public void ConverterPath_RejectMode_DuplicateKnownPropertyKeepsLastValue()
    {
        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","firstName":"Bob"}""",
            CamelCase)!;

        patch.FirstName.Should().Be("Bob");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ConverterPath_RejectMode_JsonPropertyNameOverrideRecognized()
    {
        var act = () => JsonSerializer.Deserialize<StrictJsonPropertyNamePatch>(
            """{"first_name":"Alice","bad_prop":"x"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .Where(ex => ex.Message.Contains("'bad_prop'") && !ex.Message.Contains("first_name"));
    }

    [Fact]
    public void ConverterPath_RejectMode_CaseInsensitiveMatchIsNotReportedUnknown()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"FIRSTNAME":"Alice"}""",
            options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ConverterPath_RejectMode_TreatsJsonIgnorePropertyAsUnknown()
    {
        var act = () => JsonSerializer.Deserialize<StrictJsonIgnorePatch>(
            """{"firstName":"Alice","internalNote":"secret"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .WithMessage("Unknown JSON properties on StrictJsonIgnorePatch: 'internalNote'");
    }

    [Fact]
    public void ConverterPath_BufferedRejectMode_ThrowsForUnknownProperty()
    {
        var act = () => JsonSerializer.Deserialize<StrictInitOnlyPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .WithMessage("Unknown JSON properties on StrictInitOnlyPatch: 'unknownProp'");
    }

    [Fact]
    public void ConverterPath_ParentRejectChildIgnore_Succeeds()
    {
        var patch = JsonSerializer.Deserialize<StrictParentIgnoreChildPatch>(
            """{"address":{"city":"Leeds","unknownNested":"x"}}""",
            CamelCase)!;

        patch.Address.Should().NotBeNull();
        patch.Provided.Address.Should().BeTrue();
        patch.Address!.Provided.City.Should().BeTrue();
        patch.Address.Provided.Line1.Should().BeFalse();
    }

    [Fact]
    public void ConverterPath_ParentIgnoreChildReject_ThrowsOnlyChildError()
    {
        var act = () => JsonSerializer.Deserialize<IgnoreParentStrictChildPatch>(
            """{"unknownTop":"x","address":{"unknownNested":"y"}}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .Where(ex => ex.Message.Contains("'unknownNested'") && !ex.Message.Contains("unknownTop"));
    }

    [Fact]
    public void ConverterPath_BothReject_ChildErrorSurfacesFirst()
    {
        var act = () => JsonSerializer.Deserialize<StrictNestedPatchDocument>(
            """{"unknownTop":"x","address":{"unknownNested":"y"}}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .Where(ex => ex.Message.Contains("'unknownNested'") && !ex.Message.Contains("unknownTop"));
    }

    [Fact]
    public void ConverterPath_NullNestedPatchDocument_DoesNotErrorByItself()
    {
        var act = () => JsonSerializer.Deserialize<StrictNestedPatchDocument>(
            """{"address":null,"unknownProp":"x"}""",
            CamelCase);

        act.Should().Throw<JsonException>()
            .WithMessage("Unknown JSON properties on StrictNestedPatchDocument: 'unknownProp'");
    }

    [Fact]
    public void ResolverPath_RejectMode_ThrowsForUnknownProperty()
    {
        var options = CreateResolverOptions();

        var act = () => JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ResolverPath_BufferedTypeStillFallsBackToConverterMetadata()
    {
        var options = CreateResolverOptions();

        var typeInfo = PatchlyJsonTypeInfoResolver.Default.GetTypeInfo(typeof(StrictInitOnlyPatch), options)!;

        typeInfo.Kind.Should().Be(JsonTypeInfoKind.None);
    }

    [Fact]
    public void ResolverPath_RejectMode_AcceptsValidPayload()
    {
        var options = CreateResolverOptions();

        var patch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            options)!;

        patch.FirstName.Should().Be("Alice");
        patch.Age.Should().Be(30);
        patch.WasProvided("FirstName").Should().BeTrue();
        patch.WasProvided("Age").Should().BeTrue();
    }

    [Fact]
    public void ResolverPath_IgnoreMode_OverridesGlobalDisallow()
    {
        var options = CreateResolverOptions(JsonUnmappedMemberHandling.Disallow);

        var patch = JsonSerializer.Deserialize<CustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            options)!;

        patch.FirstName.Should().Be("Alice");
        patch.WasProvided("FirstName").Should().BeTrue();
    }

    [Fact]
    public void ResolverPath_ExposesExpectedUnmappedMemberHandlingPerMode()
    {
        var options = CreateResolverOptions();

        var ignoreTypeInfo = PatchlyJsonTypeInfoResolver.Default.GetTypeInfo(typeof(CustomerPatch), options)!;
        var rejectTypeInfo = PatchlyJsonTypeInfoResolver.Default.GetTypeInfo(typeof(StrictCustomerPatch), options)!;

        ignoreTypeInfo.UnmappedMemberHandling.Should().Be(JsonUnmappedMemberHandling.Skip);
        rejectTypeInfo.UnmappedMemberHandling.Should().Be(JsonUnmappedMemberHandling.Disallow);
    }

    [Fact]
    public void ResolverAndConverterPaths_RejectTheSameUnknownPayload()
    {
        var resolverOptions = CreateResolverOptions();

        var resolverAct = () => JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            resolverOptions);
        var converterAct = () => JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","unknownProp":"value"}""",
            CamelCase);

        resolverAct.Should().Throw<JsonException>();
        converterAct.Should().Throw<JsonException>();
    }

    [Fact]
    public void ResolverAndConverterPaths_AcceptTheSameValidPayload()
    {
        var resolverOptions = CreateResolverOptions();

        var resolverPatch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            resolverOptions)!;
        var converterPatch = JsonSerializer.Deserialize<StrictCustomerPatch>(
            """{"firstName":"Alice","age":30}""",
            CamelCase)!;

        resolverPatch.FirstName.Should().Be(converterPatch.FirstName);
        resolverPatch.Age.Should().Be(converterPatch.Age);
        resolverPatch.WasProvided("FirstName").Should().Be(converterPatch.WasProvided("FirstName"));
        resolverPatch.WasProvided("Age").Should().Be(converterPatch.WasProvided("Age"));
    }
}
