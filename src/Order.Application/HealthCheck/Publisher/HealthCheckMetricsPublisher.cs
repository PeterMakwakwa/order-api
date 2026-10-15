using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Metrics;

namespace Order.Application.HealthCheck.Publisher
{
    public sealed class HealthCheckMetricsPublisher(IObservabilityManager observabilityManager) : IHealthCheckPublisher
    {
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<string, HealthReportEntry> healthCheckEntry in report.Entries)
            {
                KeyValuePair<string, object?> tag = new(MetricDimensions.HealthCheckName, healthCheckEntry.Key);
                if (healthCheckEntry.Value.Status == HealthStatus.Healthy)
                {
                    observabilityManager
                        .RecordMetric(MetricDefinitions.HealthCheckSuccessCount)
                        .Record(tags: tag);
                }
                else
                {
                    observabilityManager
                        .RecordMetric(MetricDefinitions.HealthCheckErrorCount)
                        .Record(tags: tag);
                }
            }

            return Task.CompletedTask;
        }
    }
}