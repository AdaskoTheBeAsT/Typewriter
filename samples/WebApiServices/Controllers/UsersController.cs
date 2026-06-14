using System.Threading;
using System.Threading.Tasks;
using WebApiServices.Infrastructure;
using WebApiServices.Models;

namespace WebApiServices.Controllers;

[GenerateFrontendType]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    [HttpGet("items/{id}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    public Task<UserDto> GetItemAsync([FromRoute] int id, string? search = null)
    {
        throw new NotImplementedException();
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(UserDto), 201)]
    public Task<UserDto> CreateAsync([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
