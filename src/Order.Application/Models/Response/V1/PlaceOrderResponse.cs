using Order.Domain.Enums;
using System.Text.Json.Serialization;

namespace Order.Application.Models.Response.V1
{
    /// <summary>
    /// Represents the response returned after placing an order.
    /// </summary>
    public sealed record PlaceOrderResponse
    {
        /// <summary>
        /// The correlation id of the request that placed the order.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// The unique id of the order.
        /// </summary>
        public required Guid OrderId { get; init; }

        /// <summary>
        /// The promotion code applied, if any.
        /// </summary>
        public string? PromotionCode { get; init; }

        /// <summary>
        /// The amount before the discount.
        /// </summary>
        public required decimal Subtotal { get; init; }

        /// <summary>
        /// The amount charged after the discount.
        /// </summary>
        public required decimal Total { get; init; }

        /// <summary>
        /// Completed for card and PayPal payments; AwaitingPayment for bank transfers.
        /// </summary>
        public required OrderStatus Status { get; init; }

        /// <summary>
        /// When the order was placed.
        /// </summary>
        public required DateTime DateCreated { get; init; }

        /// <summary>
        /// True when this is the stored result of an earlier request with the same Idempotency-Key
        /// (returned as the Idempotent-Replayed header, not in the body).
        /// </summary>
        [JsonIgnore]
        public bool IsReplay { get; init; }
    }
}