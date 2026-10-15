namespace Order.Application.Models.Request.V1
{
    /// <summary>
    /// Represents a request for a single order.
    /// </summary>
    public sealed record GetOrderRequest
    {
        /// <summary>
        /// The unique id of the order.
        /// </summary>
        public required Guid OrderId { get; init; }

        /// <summary>
        /// The caller's customer id: only an order of this customer is returned. Null for support staff, who can read any order.
        /// </summary>
        public int? CustomerId { get; init; }
    }
}