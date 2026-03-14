namespace DP.Worker.Services;

public interface IPaymentProcessingService
{
    Task<bool> ProcessPaymentAsync(Guid paymentId, decimal amount, string currency, string userId);
}

public class PaymentProcessingService : IPaymentProcessingService
{
    private readonly ILogger<PaymentProcessingService> _logger;
    private static readonly Random _random = new Random();

    public PaymentProcessingService(ILogger<PaymentProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ProcessPaymentAsync(Guid paymentId, decimal amount, string currency, string userId)
    {
        _logger.LogInformation("Processing payment {PaymentId} for amount {Amount} {Currency}", paymentId, amount, currency);
        
        // Simulate network/processing delay
        await Task.Delay(_random.Next(100, 1000));

        // Simulate 70% success rate
        bool success = _random.NextDouble() > 0.3;

        if (!success)
        {
            _logger.LogWarning("Payment {PaymentId} failed randomly due to simulated bank error.", paymentId);
            throw new Exception("Simulated bank decline or network timeout.");
        }

        _logger.LogInformation("Payment {PaymentId} processed successfully.", paymentId);
        return true;
    }
}
