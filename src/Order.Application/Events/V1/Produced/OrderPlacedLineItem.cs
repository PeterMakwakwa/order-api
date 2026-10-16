namespace Order.Application.Events.V1.Produced
{
    public sealed record OrderPlacedLineItem
    {
        public required int ProductId { get; init; }

        public required string ProductName { get; init; }

        public required decimal UnitPrice { get; init; }

        public required int Quantity { get; init; }
    }
}