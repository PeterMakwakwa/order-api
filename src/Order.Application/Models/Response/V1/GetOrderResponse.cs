using Order.Application.Models.Response.V1.Order;
using Order.Domain.Enums;

namespace Order.Application.Models.Response.V1
{
    /// <summary>
    /// Represents a single order with its line items.
    /// </summary>
    public sealed record GetOrderResponse
    {
        /// <summary>
        /// The unique id of the order.
        /// </summary>
        public required Guid OrderId { get; init; }

        /// <summary>
        /// The customer who placed the order.
        /// </summary>
        public required int CustomerId { get; init; }

        /// <summary>
        /// The current status of the order.
        /// </summary>
        public required OrderStatus Status { get; init; }

        /// <summary>
        /// How the order was paid.
        /// </summary>
        public required PaymentMethods PaymentMethod { get; init; }

        /// <summary>
        /// The promotion code applied, if any.
        /// </summary>
        public string? PromotionCode { get; init; }

        /// <summary>
        /// The discount applied, as a fraction (0.1 = 10%).
        /// </summary>
        public required decimal DiscountRate { get; init; }

        /// <summary>
        /// The amount before the discount.
        /// </summary>
        public required decimal Subtotal { get; init; }

        /// <summary>
        /// The amount charged after the discount.
        /// </summary>
        public required decimal Total { get; init; }

        /// <summary>
        /// When the order was placed.
        /// </summary>
        public required DateTime DateCreated { get; init; }

        /// <summary>
        /// The products ordered, with the name and price at the time of ordering.
        /// </summary>
        public required List<OrderLineItemResponse> Items { get; init; }
    }
}