using DP.Infrastructure.Database;
using DP.Infrastructure.Services;
using DP.Shared;
using DP.Shared.Events;
using DP.Shared.Models;
using DP.Worker.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace DP.Worker.Consumers;

public class PaymentRequestedConsumer : IConsumer<PaymentRequested>
{
    private readonly IPaymentProcessingService _paymentProcessor;
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    public PaymentRequestedConsumer(
        IPaymentProcessingService paymentProcessor,
        AppDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<PaymentRequestedConsumer> logger)
    {
        _paymentProcessor = paymentProcessor;
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentRequested> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Received PaymentRequested for {PaymentId}", msg.PaymentId);

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == msg.PaymentId);
        if (payment == null)
        {
            _logger.LogError("Payment {PaymentId} not found in DB. Skipping.", msg.PaymentId);
            return;
        }

        if (payment.Status == PaymentStatus.Completed)
        {
            _logger.LogInformation("Payment {PaymentId} already completed.", msg.PaymentId);
            return;
        }

        payment.Status = PaymentStatus.Processing;
        payment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogEventAsync(msg.PaymentId, Constants.AuditEvents.ProcessingStarted, new { Attempt = context.GetRetryAttempt() });

        try
        {
            await _paymentProcessor.ProcessPaymentAsync(msg.PaymentId, msg.Amount, msg.Currency, msg.UserId);

            payment.Status = PaymentStatus.Completed;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogService.LogEventAsync(msg.PaymentId, Constants.AuditEvents.Succeeded, new { amount = msg.Amount });
            
            await context.Publish(new PaymentCompleted { PaymentId = msg.PaymentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId}. Exception: {Message}", msg.PaymentId, ex.Message);
            
            var retryAttempt = context.GetRetryAttempt();
            await _auditLogService.LogEventAsync(msg.PaymentId, Constants.AuditEvents.RetryAttempt, new { Error = ex.Message, Attempt = retryAttempt });

            // If this throws, MassTransit's retry policy kicks in.
            throw;
        }
    }
}

public class PaymentRequestedConsumerDefinition : ConsumerDefinition<PaymentRequestedConsumer>
{
    public PaymentRequestedConsumerDefinition()
    {
        EndpointName = Constants.Queues.PaymentRequested;
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<PaymentRequestedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        });
    }
}
