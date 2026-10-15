namespace Order.Application.Models.Configuration.Observability
{
    [Serializable]
    public sealed class ObservabilityConfiguration
    {
        /// <summary>
        /// Name of the Meter and ActivitySource; metrics are published as {application}_{metric}.
        /// </summary>
        public required string ApplicationName { get; init; }

        /// <summary>
        /// OTLP collector endpoint (e.g. http://localhost:4317). Leave empty to disable trace export.
        /// </summary>
        public string OtlpEndpoint { get; init; } = string.Empty;
    }
}