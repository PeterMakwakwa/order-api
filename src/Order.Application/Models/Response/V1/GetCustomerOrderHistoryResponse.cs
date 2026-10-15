using Order.Application.Models.Response.V1.Order;

namespace Order.Application.Models.Response.V1
{
    /// <summary>
    /// Represents one page of a customer's order history, newest first.
    /// </summary>
    public sealed record GetCustomerOrderHistoryResponse
    {
        /// <summary>
        /// The customer the orders belong to.
        /// </summary>
        public required int CustomerId { get; init; }

        /// <summary>
        /// The number of orders per page.
        /// </summary>
        public required int PageSize { get; init; }

        /// <summary>
        /// How many orders the customer has in total, across all pages.
        /// </summary>
        public required int TotalCount { get; init; }

        /// <summary>
        /// The orders on this page, newest first.
        /// </summary>
        public required List<OrderSummaryResponse> Orders { get; init; }

        /// <summary>
        /// Pass as the cursor to get the next (older) page; null on the last page.
        /// </summary>
        public string? NextCursor { get; init; }
    }
}