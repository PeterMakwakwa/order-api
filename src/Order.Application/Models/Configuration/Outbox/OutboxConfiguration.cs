namespace Order.Application.Models.Configuration.Outbox
{
    [Serializable]
    public sealed class OutboxConfiguration
    {
        public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(10);

        public int BatchSize { get; init; } = 20;

        /// <summary>
        /// After this many failed attempts a message is no longer retried (it stays in the table for investigation).
        /// </summary>
        public int MaxAttempts { get; init; } = 5;

        /// <summary>
        /// How long a claimed message is reserved for one processor. Must comfortably exceed one email send
        /// (including its retries); if the processor dies, another one picks the message up after this.
        /// </summary>
        public TimeSpan LockDuration { get; init; } = TimeSpan.FromMinutes(5);
    }
}