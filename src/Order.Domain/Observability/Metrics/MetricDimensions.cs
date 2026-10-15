namespace Order.Domain.Observability.Metrics
{
    public static class MetricDimensions
    {
        public const string PaymentMethod = "payment_method";

        public const string PaymentGateway = "gateway";

        public const string PaymentOutcome = "outcome";

        public const string OrderStatus = "order_status";

        public const string FailureReason = "reason";

        public const string HealthCheckName = "healthcheck_name";

        public const string Endpoint = "endpoint";

        public const string HttpMethod = "method";

        public const string StatusCode = "status_code";
    }
}