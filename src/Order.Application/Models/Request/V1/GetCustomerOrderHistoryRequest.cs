namespace Order.Application.Models.Request.V1
{
    /// <summary>
    /// Represents a request for one page of a customer's order history.
    /// </summary>
    public sealed record GetCustomerOrderHistoryRequest
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        /// <summary>
        /// The customer whose orders are returned.
        /// </summary>
        public required int CustomerId { get; init; }

        /// <summary>
        /// Where to continue: the NextCursor of the previous page, or null for the first (newest) page.
        /// </summary>
        public string? Cursor { get; init; }

        /// <summary>
        /// The number of orders per page (at most <see cref="MaxPageSize"/>).
        /// </summary>
        public int PageSize { get; init; } = DefaultPageSize;
    }
}