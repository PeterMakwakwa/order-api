namespace Order.Domain.Repository.ReadModels
{
    public sealed record OrderLineItemDetail
    {
        public required int ProductId { get; init; }

        public required string ProductName { get; init; }

        public required decimal UnitPrice { get; init; }

        public required int Quantity { get; init; }
    }
}