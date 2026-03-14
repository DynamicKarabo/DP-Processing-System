namespace DP.Shared.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
