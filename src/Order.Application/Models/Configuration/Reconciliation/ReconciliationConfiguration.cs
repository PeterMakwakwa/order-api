namespace Order.Application.Models.Configuration.Reconciliation
{
    [Serializable]
    public sealed class ReconciliationConfiguration
    {
        public TimeSpan PollingInterval { get; init; } = TimeSpan.FromMinutes(1);

        public int BatchSize { get; init; } = 20;

        /// <summary>
        /// How long an order may stay PendingPayment before it is treated as abandoned mid-charge and charged again.
        /// Must comfortably exceed a whole charge (the payment gateway timeout, including its retries), so an order that is
        /// still being charged is never picked up.
        /// </summary>
        public TimeSpan StalledPaymentTimeout { get; init; } = TimeSpan.FromMinutes(5);
    }
}