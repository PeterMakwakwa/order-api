using Order.Domain.Enums;

namespace Order.Application.Events.V1.Produced
{
    /// <summary>
    /// Stored in the queueOrderPlaced outbox when an order's payment is accepted; the confirmation email is built from it.
    /// Version 2 added the line items, subtotal and promotion code. Version 1 messages (written before) still deserialize,
    /// with no items, so messages already waiting in the outbox are still sent.
    /// </summary>
    public sealed record OrderPlaced
    {
        public const int Version = 2;

        public const int MinimumSupportedVersion = 1;

        public required Guid Id { get; init; }

        public required string CorrelationId { get; init; }

        public required int CustomerId { get; init; }

        public required string CustomerEmail { get; init; }

        public required decimal Total { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }

        public required OrderStatus Status { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }

        /// <summary>
        /// The total before the discount. Null in version 1 messages.
        /// </summary>
        public decimal? Subtotal { get; init; }

        public string? PromotionCode { get; init; }

        /// <summary>
        /// What was ordered, as it was priced when the order was placed. Empty in version 1 messages.
        /// </summary>
        public List<OrderPlacedLineItem> Items { get; init; } = [];
    }
}