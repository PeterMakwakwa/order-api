using Microsoft.AspNetCore.Builder;
using Prometheus;

namespace Order.Infrastructure.Observability.Extensions
{
    public static class MetricsEndpointExtensions
    {
        public const string MetricsEndpoint = "/metrics";

        /// <summary>
        /// prometheus-net's built-in meter listener only starts on the first scrape, so everything recorded
        /// before it would be lost. It is replaced by one started at application startup (once per process).
        /// </summary>
        private static readonly Lazy<IDisposable> MeterListener = new(() =>
        {
            Prometheus.Metrics.SuppressDefaultMetrics(new SuppressDefaultMetricOptions { SuppressMeters = true });
            return MeterAdapter.StartListening();
        });

        /// <summary>
        /// Prometheus scrape endpoint. Every System.Diagnostics.Metrics Meter is published, so the custom metrics
        /// recorded through <see cref="Interfaces.IObservabilityManager"/> appear here as {application}_{metric}
        /// (e.g. order_api_place_order_success_count).
        /// </summary>
        public static IApplicationBuilder AddMetricsEndpoint(this IApplicationBuilder applicationBuilder)
        {
            _ = MeterListener.Value;

            return applicationBuilder
                .UseMetricServer(MetricsEndpoint);
        }
    }
}