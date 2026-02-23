using System.Text.Json.Serialization;

namespace Patchly.IntegrationTests;

[PatchDocument]
public partial class UpdateCustomerPatch
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }
}

[PatchDocument]
public partial class BufferedPatch
{
    public string? Name { get; init; }
    public int? Value { get; init; }
}

[PatchDocument]
public partial class JsonPropertyNamePatch
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
