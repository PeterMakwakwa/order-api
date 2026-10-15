using Order.Domain.Enums;

namespace Order.Domain.Repository.ReadModels
{
    public sealed record OrderDetail
    {
        public required Guid Id { get; init; }

        public required int CustomerId { get; init; }

        public required OrderStatus Status { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }

        public string? PromotionCode { get; init; }

        public required decimal DiscountRate { get; init; }

        public required decimal Subtotal { get; init; }

        public required decimal Total { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }

        public required IReadOnlyList<OrderLineItemDetail> LineItems { get; init; }
    }
}