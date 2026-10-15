namespace Order.Application.Models.Response.V1.Order
{
    /// <summary>
    /// A product on an order, with the name and price at the time of ordering.
    /// </summary>
    public sealed record OrderLineItemResponse
    {
        /// <summary>
        /// The product Id.
        /// </summary>
        public required int ProductId { get; init; }

        /// <summary>
        /// The product name when the order was placed.
        /// </summary>
        public required string ProductName { get; init; }

        /// <summary>
        /// The unit price when the order was placed.
        /// </summary>
        public required decimal UnitPrice { get; init; }

        /// <summary>
        /// The quantity ordered.
        /// </summary>
        public required int Quantity { get; init; }
    }
}