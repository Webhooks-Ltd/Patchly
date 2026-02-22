using Microsoft.AspNetCore.Mvc;
using Patchly;

namespace ControllerApi;

[ApiController]
[Route("[controller]")]
public class ProductsController(IPatchApplier patchApplier) : ControllerBase
{
    private static readonly Dictionary<int, Product> Products = new()
    {
        [1] = new() { Id = 1, Name = "Widget", Price = 9.99m, Description = "A useful widget" },
        [2] = new() { Id = 2, Name = "Gadget", Price = 19.99m, Description = "A fancy gadget" },
    };

    [HttpGet("{id}")]
    public IActionResult Get(int id) =>
        Products.TryGetValue(id, out var product) ? Ok(product) : NotFound();

    [HttpPatch("{id}")]
    public IActionResult Patch(int id, ProductPatch patch)
    {
        if (!Products.TryGetValue(id, out var product))
            return NotFound();

        patchApplier.Apply(patch, product);
        return Ok(product);
    }
}
