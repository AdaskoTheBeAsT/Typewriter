namespace SimpleApi.Models;

public sealed class User
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public string? Email { get; init; }

    public IReadOnlyList<Order> Orders { get; init; } = [];
}
