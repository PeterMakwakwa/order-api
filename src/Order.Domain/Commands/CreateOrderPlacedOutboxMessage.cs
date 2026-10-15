namespace Order.Domain.Commands
{
    public sealed record CreateOrderPlacedOutboxMessage
    {
        public required int Version { get; init; }

        public required string Message { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }
    }
}