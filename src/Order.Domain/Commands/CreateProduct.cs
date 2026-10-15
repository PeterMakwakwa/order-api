namespace Order.Domain.Commands
{
    public sealed record CreateProduct
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public required decimal Price { get; init; }
    }
}