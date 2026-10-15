namespace Order.Domain.Commands
{
    public sealed record CreatePromotion
    {
        public required string Code { get; init; }

        /// <summary>
        /// Fraction between 0 (inclusive) and 1 (exclusive), e.g. 0.10 = 10% off.
        /// </summary>
        public required decimal DiscountRate { get; init; }

        public required bool IsActive { get; init; }

        public required DateTimeOffset ValidFrom { get; init; }

        public DateTimeOffset? ValidTo { get; init; }
    }
}