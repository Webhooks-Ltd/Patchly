using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Patchly.IntegrationTests;

public class IntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PatchEndpoint_PartialJson_IdentifiesProvidedProperties()
    {
        var content = new StringContent("""{"firstName":"Alice","age":30}""", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/api/customers/1", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("providedFirstName").GetBoolean().Should().BeTrue();
        result.GetProperty("providedAge").GetBoolean().Should().BeTrue();
        result.GetProperty("providedLastName").GetBoolean().Should().BeFalse();
        result.GetProperty("firstName").GetString().Should().Be("Alice");
        result.GetProperty("age").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task PatchEndpoint_DistinguishesNullFromAbsent()
    {
        var content = new StringContent("""{"firstName":null}""", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/api/customers/1", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("providedFirstName").GetBoolean().Should().BeTrue();
        result.GetProperty("firstName").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("providedLastName").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task OpenApiSchema_NoTrackingProperties()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain("providedProperties");
        json.Should().NotContain("_providedProperties");
        json.Should().NotContain("wasProvided");
        json.Should().NotContain("ProvidedSet");
    }

    [Fact]
    public async Task OpenApiSchema_NoRequiredProperties()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("UpdateCustomerPatch", out var schema).Should().BeTrue(
            "UpdateCustomerPatch schema should exist in OpenAPI document");
        schema.TryGetProperty("required", out _).Should().BeFalse(
            "patch DTOs should not have required properties in OpenAPI schema");
    }

    [Fact]
    public async Task MinimalApiEndpoint_WorksWithPatchDocuments()
    {
        var content = new StringContent("""{"firstName":"Bob","age":25}""", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/minimal/customers/42", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("id").GetString().Should().Be("42");
        result.GetProperty("providedFirstName").GetBoolean().Should().BeTrue();
        result.GetProperty("firstName").GetString().Should().Be("Bob");
    }

    [Fact]
    public async Task ControllerEndpoint_WorksWithFromBodyPatchDocuments()
    {
        var content = new StringContent("""{"lastName":"Smith"}""", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/api/customers/1", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("providedLastName").GetBoolean().Should().BeTrue();
        result.GetProperty("providedFirstName").GetBoolean().Should().BeFalse();
        result.GetProperty("lastName").GetString().Should().Be("Smith");
    }

    [Fact]
    public async Task OpenApiSchema_StreamingPathType_HasProperties()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty("UpdateCustomerPatch");
        var properties = schema.GetProperty("properties");

        properties.TryGetProperty("firstName", out _).Should().BeTrue();
        properties.TryGetProperty("lastName", out _).Should().BeTrue();
        properties.TryGetProperty("age", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApiSchema_PropertiesAreNullable()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty("UpdateCustomerPatch");
        var properties = schema.GetProperty("properties");

        var firstName = properties.GetProperty("firstName");
        SchemaAllowsNull(firstName).Should().BeTrue("firstName should be nullable");

        var age = properties.GetProperty("age");
        SchemaAllowsNull(age).Should().BeTrue("age should be nullable");
    }

    [Fact]
    public async Task OpenApiSchema_TrackingInfrastructureNotInSchema()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty("UpdateCustomerPatch");
        var properties = schema.GetProperty("properties");

        properties.TryGetProperty("_providedProperties", out _).Should().BeFalse();
        properties.TryGetProperty("wasProvided", out _).Should().BeFalse();
        properties.TryGetProperty("providedProperties", out _).Should().BeFalse();
        properties.TryGetProperty("provided", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OpenApiSchema_NestedConverterNotInSchema()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain("JsonConverter");
        json.Should().NotContain("UpdateCustomerPatchJsonConverter");
    }

    [Fact]
    public async Task OpenApiSchema_BufferedPathType_HasEmptySchema()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty("BufferedPatch");
        schema.TryGetProperty("properties", out _).Should().BeFalse(
            "buffered-path types should have empty schema (known limitation)");
    }

    [Fact]
    public async Task OpenApiSchema_WithoutAddPatchly_StreamingPathHasEmptySchema()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(AppContext.BaseDirectory);
                builder.ConfigureServices(services =>
                {
                    services.ConfigureHttpJsonOptions(o =>
                    {
                        for (var i = o.SerializerOptions.TypeInfoResolverChain.Count - 1; i >= 0; i--)
                        {
                            if (o.SerializerOptions.TypeInfoResolverChain[i] is Patchly.PatchlyJsonTypeInfoResolver)
                                o.SerializerOptions.TypeInfoResolverChain.RemoveAt(i);
                        }
                    });
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty("UpdateCustomerPatch");
        schema.TryGetProperty("properties", out _).Should().BeFalse(
            "without AddPatchly, streaming-path types should have empty schema");
    }

    [Fact]
    public async Task EndToEnd_PatchRoundTrip_WithAddPatchly()
    {
        var content = new StringContent("""{"firstName":"Alice","age":null}""", Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync("/minimal/customers/99", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("providedFirstName").GetBoolean().Should().BeTrue();
        result.GetProperty("providedAge").GetBoolean().Should().BeTrue();
        result.GetProperty("providedLastName").GetBoolean().Should().BeFalse();
        result.GetProperty("firstName").GetString().Should().Be("Alice");
        result.GetProperty("age").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task EndToEnd_PatchWithoutAddPatchly_ConverterFallbackWorks()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(AppContext.BaseDirectory);
                builder.ConfigureServices(services =>
                {
                    services.ConfigureHttpJsonOptions(o =>
                    {
                        for (var i = o.SerializerOptions.TypeInfoResolverChain.Count - 1; i >= 0; i--)
                        {
                            if (o.SerializerOptions.TypeInfoResolverChain[i] is Patchly.PatchlyJsonTypeInfoResolver)
                                o.SerializerOptions.TypeInfoResolverChain.RemoveAt(i);
                        }
                    });
                });
            });
        using var client = factory.CreateClient();

        var content = new StringContent("""{"firstName":"Bob","age":25}""", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync("/minimal/customers/1", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        result.GetProperty("providedFirstName").GetBoolean().Should().BeTrue();
        result.GetProperty("firstName").GetString().Should().Be("Bob");
    }

    private static bool SchemaAllowsNull(JsonElement schema)
    {
        if (schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean())
            return true;

        if (schema.TryGetProperty("type", out var typeEl))
        {
            if (typeEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in typeEl.EnumerateArray())
                {
                    if (t.GetString() == "null") return true;
                }
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf))
        {
            foreach (var item in anyOf.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "null")
                    return true;
            }
        }

        return false;
    }
}
