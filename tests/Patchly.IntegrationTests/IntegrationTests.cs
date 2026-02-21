using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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
        if (schemas.TryGetProperty("UpdateCustomerPatch", out var schema))
        {
            schema.TryGetProperty("required", out _).Should().BeFalse(
                "patch DTOs should not have required properties in OpenAPI schema");
        }
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
}
