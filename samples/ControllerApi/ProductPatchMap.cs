using Patchly;

namespace ControllerApi;

public class ProductPatchMap : PatchMap<ProductPatch, Product>
{
    public override void Apply(ProductPatch patch, Product target)
    {
        if (patch.Provided.Name) target.Name = patch.Name;
        if (patch.Provided.Price) target.Price = patch.Price ?? 0;
        if (patch.Provided.Description) target.Description = patch.Description;
    }
}
