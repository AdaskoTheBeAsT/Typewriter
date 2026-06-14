namespace NullableModels.Models;

public sealed class Profile
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public int? Score { get; init; }

    public DateOnly? BirthDate { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyDictionary<string, decimal> Balances { get; init; } = new Dictionary<string, decimal>();
}
