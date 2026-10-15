using Order.Domain.Enums;

namespace Order.Domain.Interfaces.Payments.Models
{
    public sealed record PaymentRequest
    {
        /// <summary>
        /// Also sent to the provider as the idempotency key, which makes retries safe.
        /// </summary>
        public required Guid OrderId { get; init; }

        public required int CustomerId { get; init; }

        public required decimal Amount { get; init; }

        public required string Currency { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }
    }
}