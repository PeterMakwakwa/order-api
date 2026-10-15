namespace Order.Domain.Observability.Models.Metrics.Definitions
{
    public abstract class MetricDefinition(string name)
    {
        public string Name { get; } = name;
    }

    /// <summary>
    /// A monotonically increasing value (requests, failures, amounts).
    /// </summary>
    public sealed class CounterMetricDefinition(string name) : MetricDefinition(name);

    /// <summary>
    /// A distribution of values (durations, sizes).
    /// </summary>
    public sealed class HistogramMetricDefinition(string name) : MetricDefinition(name);

    /// <summary>
    /// The last recorded value per tag set (queue depth, rolling totals).
    /// </summary>
    public sealed class GaugeMetricDefinition(string name) : MetricDefinition(name);
}