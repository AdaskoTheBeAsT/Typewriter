namespace WebApiServices.Models;

[WebApiServices.Infrastructure.GenerateFrontendType]
public sealed class UserDto
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
