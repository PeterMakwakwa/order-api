namespace Order.Application.Models.Configuration.Payments
{
    [Serializable]
    public sealed class PaymentGatewayConfiguration
    {
        public required string BaseUrl { get; init; }

        /// <summary>
        /// Secret: supply through user-secrets / environment variables / a vault, never appsettings.json.
        /// </summary>
        public string ApiKey { get; init; } = string.Empty;

        /// <summary>
        /// Answers every charge with 202 Accepted instead of calling the provider (local development).
        /// </summary>
        public bool UseMockProvider { get; init; }

        public int RetryCount { get; init; } = 3;

        public double RetryBackoffBaseSeconds { get; init; } = 2;

        public int CircuitBreakerFailureThreshold { get; init; } = 5;

        public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);

        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    }
}