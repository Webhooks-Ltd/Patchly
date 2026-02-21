namespace Patchly.IntegrationTests;

[PatchDocument]
public partial class UpdateCustomerPatch
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }
}
