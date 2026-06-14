namespace SimpleApi.Models;

public sealed record Order(
    int Id,
    decimal Total,
    OrderStatus Status,
    DateTime CreatedAt);
