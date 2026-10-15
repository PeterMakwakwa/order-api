namespace Order.Application.Models.Configuration.RateLimiting
{
    /// <summary>
    /// Fixed window per customer (or per client IP for unauthenticated calls) on the order endpoints.
    /// </summary>
    [Serializable]
    public sealed class RateLimitingConfiguration
    {
        public int PermitLimit { get; init; } = 60;

        public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    }
}