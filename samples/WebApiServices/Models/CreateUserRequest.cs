namespace WebApiServices.Models;

[WebApiServices.Infrastructure.GenerateFrontendType]
public sealed class CreateUserRequest
{
    public required string DisplayName { get; init; }
}
