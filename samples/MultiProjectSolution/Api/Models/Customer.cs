using MultiProjectSolution.Contracts.Models;

namespace MultiProjectSolution.Api.Models;

public sealed class Customer
{
    public required string Id { get; init; }

    public required Address ShippingAddress { get; init; }
}
