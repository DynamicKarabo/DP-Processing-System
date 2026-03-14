namespace DP.Shared;

public static class Constants
{
    public static class IdempotencyHeaders
    {
        public const string Key = "Idempotency-Key";
    }

    public static class AuditEvents
    {
        public const string Created = "PaymentCreated";
        public const string ProcessingStarted = "PaymentProcessingStarted";
        public const string Succeeded = "PaymentSucceeded";
        public const string Failed = "PaymentFailed";
        public const string RetryAttempt = "RetryAttempt";
        public const string MovedToDLQ = "MovedToDLQ";
    }

    public static class Queues
    {
        public const string PaymentRequested = "payment-requested";
    }
}
