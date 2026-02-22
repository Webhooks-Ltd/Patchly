using Patchly;

namespace ControllerApi;

[PatchDocument]
public partial class ProductPatch
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
