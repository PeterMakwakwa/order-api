using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Models.Metrics.Definitions;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Order.Infrastructure.Observability.Metrics
{
    internal sealed class CounterMetric(Meter meter, CounterMetricDefinition definition) : IMetric
    {
        private readonly Counter<long> _counter = meter.CreateCounter<long>(definition.Name);

        public void Record(long value = 1, params ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            _counter.Add(value, tags);
        }
    }

    internal sealed class HistogramMetric(Meter meter, HistogramMetricDefinition definition) : IMetric
    {
        private readonly Histogram<long> _histogram = meter.CreateHistogram<long>(definition.Name);

        public void Record(long value = 1, params ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            _histogram.Record(value, tags);
        }
    }

    /// <summary>
    /// Observable gauge that reports the last value recorded for each distinct tag set.
    /// </summary>
    internal sealed class GaugeMetric : IMetric
    {
        private readonly ConcurrentDictionary<string, (long Value, KeyValuePair<string, object?>[] Tags)> _values = new();

        public GaugeMetric(Meter meter, GaugeMetricDefinition definition)
        {
            meter.CreateObservableGauge(definition.Name, Observe);
        }

        public void Record(long value = 1, params ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            KeyValuePair<string, object?>[] tagArray = tags.ToArray();
            string key = string.Join("|", tagArray.Select(tag => $"{tag.Key}={tag.Value}"));

            _values[key] = (value, tagArray);
        }

        private IEnumerable<Measurement<long>> Observe()
        {
            return [.. _values.Values.Select(entry => new Measurement<long>(entry.Value, entry.Tags))];
        }
    }
}