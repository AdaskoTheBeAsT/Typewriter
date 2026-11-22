namespace Issue146;

public abstract class BaseQuery<TId>
{
    public TId CorrelationId = default!;

    public TId Id { get; set; } = default!;
}

public sealed class CustomerSearchQuery : BaseQuery<Guid>
{
    public bool IncludeInactive;

    public string SearchTerm { get; set; } = string.Empty;

    public int PageSize { get; set; }

    internal string InternalCursor { get; set; } = string.Empty;
}

public sealed class CustomersController
{
    public Task<IReadOnlyList<string>> Search(CustomerSearchQuery query, int retryCount) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
