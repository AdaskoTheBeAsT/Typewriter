namespace RecordsAndEnums.Models;

public sealed record Product(
    Guid Id,
    string Name,
    ProductKind Kind,
    Price Price);
