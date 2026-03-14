namespace DP.Shared.Events;

public record PaymentCompleted
{
    public Guid PaymentId { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}
