namespace Order.Application.Models.Request.V1.Order
{
    /// <summary>
    /// A product and the quantity being ordered.
    /// </summary>
    public sealed record OrderItem
    {
        /// <summary>
        /// The product Id. Must exist in the product catalogue.
        /// </summary>
        public required int ProductId { get; init; }

        /// <summary>
        /// The quantity ordered. Must be greater than zero.
        /// </summary>
        public required int Quantity { get; init; }
    }
}