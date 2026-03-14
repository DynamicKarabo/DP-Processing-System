# System Design

## Architecture

```
Client -> [ API ] -> PostgreSQL (Payments, Events)
            |
            v
        RabbitMQ
            |
            v
     [ Worker ] -> External Bank / Processing
```

## Database
- `Payments`
- `IdempotencyKeys`
- `AuditLogs`

## Message Broker
- RabbitMQ passing `PaymentRequested`, `PaymentCompleted`, `PaymentFailed` events.
- Retry queue with exponential backoff on failure.

## Tech Stack
- .NET 8 Web API & Worker Services
- PostgreSQL via EF Core
- RabbitMQ via MassTransit
- Serilog for logging
- Prometheus & Grafana for metrics
