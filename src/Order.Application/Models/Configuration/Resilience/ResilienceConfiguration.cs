namespace Order.Application.Models.Configuration.Resilience
{
    /// <summary>
    /// Payment gateways configure their resilience per gateway (ApplicationConfiguration:PaymentGateways).
    /// </summary>
    [Serializable]
    public sealed class ResilienceConfiguration
    {
        public required RetryResilienceConfiguration Email { get; init; }
    }

    [Serializable]
    public sealed class RetryResilienceConfiguration
    {
        public int MaxRetryAttempts { get; init; } = 3;

        public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    }
}