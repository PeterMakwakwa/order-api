namespace Order.Application.Constants
{
    public static class RequestHeaderKeys
    {
        public const string CorrelationId = "CorrelationId";

        public const string IdempotencyKey = "Idempotency-Key";
    }
}