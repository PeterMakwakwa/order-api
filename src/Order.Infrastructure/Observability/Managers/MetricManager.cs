using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Models.Metrics.Definitions;
using Order.Infrastructure.Observability.Messages;
using Order.Infrastructure.Observability.Metrics;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Order.Infrastructure.Observability.Managers
{
    /// <summary>
    /// Creates each instrument once per name and reuses it afterwards.
    /// </summary>
    internal sealed class MetricManager(Meter meter)
    {
        private readonly ConcurrentDictionary<string, IMetric> _metrics = new();

        internal IMetric GetOrCreate(MetricDefinition definition)
        {
            return _metrics.GetOrAdd(definition.Name, _ => Create(definition));
        }

        private IMetric Create(MetricDefinition definition)
        {
            return definition switch
            {
                CounterMetricDefinition counter => new CounterMetric(meter, counter),
                HistogramMetricDefinition histogram => new HistogramMetric(meter, histogram),
                GaugeMetricDefinition gauge => new GaugeMetric(meter, gauge),
                _ => throw new InvalidOperationException(ErrorMessages.UnsupportedMetricTypeErrorMessage)
            };
        }
    }
}