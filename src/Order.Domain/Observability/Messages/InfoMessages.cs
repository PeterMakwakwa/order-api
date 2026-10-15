namespace Order.Domain.Observability.Messages
{
    public static class InfoMessages
    {
        public const string MethodStarted = "Method started.";
        public const string PlaceOrderTrace = "Place order";
        public const string OrderPlaced = "Order {0} placed with status {1}.";
        public const string PaymentChargeCompleted = "Payment for order {0} via {1} completed with outcome {2} in {3} ms.";
        public const string OutboxMessageProcessed = "Outbox message {0} processed.";
        public const string OutboxMessageClaimedElsewhere = "Outbox message {0} was claimed by another processor; skipped.";
        public const string PaymentReconciled = "Order {0} was left PendingPayment since {1:O}; charged again with the same idempotency key and settled as {2}.";
        public const string PaymentReconciliationSkipped = "Order {0} was settled by another request or processor while being reconciled; skipped.";
        public const string PaymentRetried = "Payment for order {0} is being retried after a provider error.";
        public const string OrderReplayed = "Order {0} replayed for an earlier request with the same idempotency key.";
        public const string RequestCompleted = "{0} {1} responded {2} in {3} ms.";
        public const string EmailLogged = "No SMTP host configured; email to {0} with subject '{1}' was logged instead of sent: {2}";
    }
}