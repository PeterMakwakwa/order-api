using Order.Application.Models.Request.V1.Order;
using Order.Domain.Commands;
using Order.Domain.Enums;
using System.Text.Json.Serialization;

namespace Order.Application.Models.Request.V1
{
    /// <summary>
    /// Represents a request to place a new order for the authenticated customer.
    /// </summary>
    public sealed record PlaceOrderRequest
    {
        /// <summary>
        /// The authenticated customer, taken from the access token (never from the request body).
        /// </summary>
        [JsonIgnore]
        public int CustomerId { get; init; }

        /// <summary>
        /// The address the order confirmation is sent to.
        /// </summary>
        public required string CustomerEmail { get; init; }

        /// <summary>
        /// The products and quantities being ordered. Repeated products are merged.
        /// </summary>
        public required List<OrderItem> Items { get; init; }

        /// <summary>
        /// How the customer is paying.
        /// </summary>
        public required PaymentMethods PaymentMethod { get; init; }

        /// <summary>
        /// Optional promotion code. The discount it grants is looked up on the server.
        /// </summary>
        public string? PromotionCode { get; init; }

        public CreateOrder BuildCommand(string correlationId, string idempotencyKey, string requestHash,
            string? promotionCode, decimal discountRate, List<CreateOrderLineItem> lineItems, DateTimeOffset createdDate)
        {
            return new CreateOrder
            {
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                CustomerId = CustomerId,
                CustomerEmail = CustomerEmail,
                PaymentMethod = PaymentMethod,
                PromotionCode = promotionCode,
                DiscountRate = discountRate,
                CreatedDate = createdDate,
                LineItems = lineItems
            };
        }
    }
}