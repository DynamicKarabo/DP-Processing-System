using DP.Infrastructure.Database;
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
        await _auditLogService.LogEventAsync(msg.PaymentId, "PaymentProcessingStarted", new { Attempt = context.GetRetryAttempt() });

        try
        {
            await _paymentProcessor.ProcessPaymentAsync(msg.PaymentId, msg.Amount, msg.Currency, msg.UserId);

            payment.Status = PaymentStatus.Completed;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogService.LogEventAsync(msg.PaymentId, "PaymentSucceeded", new { amount = msg.Amount });
            
            await context.Publish(new PaymentCompleted { PaymentId = msg.PaymentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId}. Exception: {Message}", msg.PaymentId, ex.Message);
            
            var retryAttempt = context.GetRetryAttempt();
            await _auditLogService.LogEventAsync(msg.PaymentId, "RetryAttempt", new { Error = ex.Message, Attempt = retryAttempt });

            // If this throws, MassTransit's retry policy (configured in Program.cs) kicks in.
            throw;
        }
    }
}

public class PaymentRequestedConsumerDefinition : ConsumerDefinition<PaymentRequestedConsumer>
{
    public PaymentRequestedConsumerDefinition()
    {
        // Limit max concurrent messages
        EndpointName = "payment_requested";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<PaymentRequestedConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            // 5 retries with exponential backoff: 1s, 2s, 4s, 8s, 16s
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        });
        
        // On max retries exhausted, MassTransit automatically moves to a queue named payment_requested_error (which acts as a DLQ)
    }
}
