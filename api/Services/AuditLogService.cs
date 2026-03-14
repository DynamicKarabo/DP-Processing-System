using DP.Infrastructure.Database;
using DP.Shared.Models;
using System.Text.Json;

namespace DP.Api.Services;

public interface IAuditLogService
{
    Task LogEventAsync<T>(Guid paymentId, string eventType, T payload);
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;

    public AuditLogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogEventAsync<T>(Guid paymentId, string eventType, T payload)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload)
        };

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();
    }
}
