using Order.Domain.Enums;

namespace Order.Domain.Commands
{
    public sealed record CreateOrder
    {
        public required string CorrelationId { get; init; }

        /// <summary>
        /// The client's Idempotency-Key header; unique per customer.
        /// </summary>
        public required string IdempotencyKey { get; init; }

        /// <summary>
        /// SHA-256 (hex) of the request, so a reused idempotency key with a different request can be detected.
        /// </summary>
        public required string RequestHash { get; init; }

        public required int CustomerId { get; init; }

        public required string CustomerEmail { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }

        /// <summary>
        /// The applied promotion, if any. The discount rate comes from the promotion, never from the client.
        /// </summary>
        public string? PromotionCode { get; init; }

        /// <summary>
        /// Fraction between 0 (no discount) and 1 (exclusive), e.g. 0.10 = 10% off.
        /// </summary>
        public required decimal DiscountRate { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }

        public required List<CreateOrderLineItem> LineItems { get; init; }
    }
}