using Order.Domain.Enums;

namespace Order.Domain.Repository.ReadModels
{
    public sealed record OrderSummary
    {
        public required Guid Id { get; init; }

        /// <summary>
        /// The internal row id; breaks ties between orders placed at the same moment when paging.
        /// </summary>
        public required long Position { get; init; }

        public required decimal Total { get; init; }

        public required OrderStatus Status { get; init; }

        public required PaymentMethods PaymentMethod { get; init; }

        public required DateTimeOffset CreatedDate { get; init; }
    }
}