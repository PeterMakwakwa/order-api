namespace Order.Domain.Observability.Messages
{
    public static class WarningMessages
    {
        public const string PaymentFailed = "Payment for order {0} failed with outcome {1}: {2}";
        public const string PaymentProviderUnreachable = "The payment provider {0} could not be reached for order {1}.";
        public const string PaymentGatewayRetry = "Retrying payment gateway {0}, attempt {1}, after {2} ms (status {3}).";
        public const string PaymentGatewayCircuitOpened = "Payment gateway {0} circuit opened for {1} ms (status {2}).";
        public const string EmailSendRetry = "Retrying email delivery, attempt {0}, after {1} ms.";
        public const string OutboxMessageFailed = "Outbox message {0} failed on attempt {1}.";
        public const string OutboxMessageVersionUnsupported = "Outbox message {0} has unsupported version {1}.";
    }
}