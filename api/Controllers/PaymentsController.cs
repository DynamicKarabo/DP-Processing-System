using DP.Api.Services;
using DP.Infrastructure.Database;
using DP.Shared.Events;
using DP.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace DP.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuditLogService _auditLogService;

    public PaymentsController(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _auditLogService = auditLogService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentDto request)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be greater than zero");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Currency = request.Currency,
            UserId = request.UserId,
            Status = PaymentStatus.Pending
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogEventAsync(payment.Id, "PaymentCreated", payment);

        var paymentRequestedEvent = new PaymentRequested
        {
            PaymentId = payment.Id,
            Amount = payment.Amount,
            Currency = payment.Currency,
            UserId = payment.UserId
        };

        await _publishEndpoint.Publish(paymentRequestedEvent);

        return Accepted(new { payment_id = payment.Id, status = payment.Status.ToString().ToLower() });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        var payment = await _dbContext.Payments.FindAsync(id);

        if (payment == null)
            return NotFound();

        return Ok(new { payment_id = payment.Id, status = payment.Status.ToString().ToLower() });
    }
}

public class CreatePaymentDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
