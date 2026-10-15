namespace Order.Domain.Observability.Interfaces
{
    public interface IMetric
    {
        public void Record(long value = 1, params ReadOnlySpan<KeyValuePair<string, object?>> tags);
    }
}