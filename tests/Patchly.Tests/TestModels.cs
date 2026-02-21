using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Patchly.Tests;

[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }
}

[PatchDocument]
public partial class AllTypesPatch
{
    public string? StringProp { get; set; }
    public int? IntProp { get; set; }
    public bool? BoolProp { get; set; }
    public DateTime? DateTimeProp { get; set; }
    public DateTimeOffset? DateTimeOffsetProp { get; set; }
    public Guid? GuidProp { get; set; }
    public TestEnum? EnumProp { get; set; }
    public decimal? DecimalProp { get; set; }
    public double? DoubleProp { get; set; }
    public NestedObject? NestedProp { get; set; }
    public List<string>? ListProp { get; set; }
    public string[]? ArrayProp { get; set; }
    public Dictionary<string, int>? DictionaryProp { get; set; }
}

public enum TestEnum
{
    None,
    ValueA,
    ValueB
}

public class NestedObject
{
    public string? Name { get; set; }
}

[PatchDocument]
public partial class JsonPropertyNamePatch
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

[PatchDocument]
public partial class JsonIgnorePatch
{
    public string? FirstName { get; set; }
    [JsonIgnore]
    public string? InternalNote { get; set; }
}

[PatchDocument]
public partial class AddressPatch
{
    public string? Line1 { get; set; }
    public string? City { get; set; }
}

[PatchDocument]
public partial class NestedPatchDocument
{
    public string? FirstName { get; set; }
    public AddressPatch? Address { get; set; }
}

[PatchDocument]
public partial class JsonIncludePatch
{
    public string? PublicName { get; set; }
    [JsonInclude]
    internal string? SecretCode { get; set; }
}

[PatchDocument]
public partial class JsonNumberHandlingPatch
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Count { get; set; }
    public string? Name { get; set; }
}

[PatchDocument]
public partial class RequiredPatch
{
    public required string? FirstName { get; set; }
    public string? LastName { get; set; }
}
