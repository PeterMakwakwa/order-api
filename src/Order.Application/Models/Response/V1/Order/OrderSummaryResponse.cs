using Order.Domain.Enums;

namespace Order.Application.Models.Response.V1.Order
{
    /// <summary>
    /// A summary of a single order.
    /// </summary>
    public sealed record OrderSummaryResponse
    {
        /// <summary>
        /// The unique id of the order.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// The amount charged after the discount.
        /// </summary>
        public required decimal Total { get; init; }

        /// <summary>
        /// The current status of the order.
        /// </summary>
        public required OrderStatus Status { get; init; }

        /// <summary>
        /// How the order was paid.
        /// </summary>
        public required PaymentMethods PaymentMethod { get; init; }

        /// <summary>
        /// When the order was placed.
        /// </summary>
        public required DateTime DateCreated { get; init; }
    }
}