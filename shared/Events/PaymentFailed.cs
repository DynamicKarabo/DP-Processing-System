namespace DP.Shared.Events;

public record PaymentFailed
{
    public Guid PaymentId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;
}
