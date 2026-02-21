using Microsoft.AspNetCore.Mvc;

namespace Patchly.IntegrationTests;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    [HttpPatch("{id}")]
    public IActionResult Patch(string id, [FromBody] UpdateCustomerPatch patch)
    {
        var result = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["providedFirstName"] = patch.Provided.FirstName,
            ["providedLastName"] = patch.Provided.LastName,
            ["providedAge"] = patch.Provided.Age,
            ["firstName"] = patch.FirstName,
            ["lastName"] = patch.LastName,
            ["age"] = patch.Age
        };
        return Ok(result);
    }
}
