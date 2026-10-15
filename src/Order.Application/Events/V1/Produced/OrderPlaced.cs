using Order.Domain.Enums;

namespace Order.Application.Events.V1.Produced
{
    public sealed record OrderPlaced
    {
        public const int Version = 1;

        public required Guid Id { get; init; }

        public required string CorrelationId { get; init; }

        public required int CustomerId { get; init; }

        public required string CustomerEmail { get; init; }

        public required decimal Total { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }

        public required OrderStatus Status { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }
    }
}