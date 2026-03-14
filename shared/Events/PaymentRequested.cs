namespace DP.Shared.Events;

public record PaymentRequested
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
}
