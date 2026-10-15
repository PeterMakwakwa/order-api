using Order.Domain.Commands;

namespace Order.Tests.Setup
{
    public static class DefaultPromotions
    {
        private static readonly DateTimeOffset LongAgo = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static readonly CreatePromotion Welcome10 = new() { Code = "WELCOME10", DiscountRate = 0.10m, IsActive = true, ValidFrom = LongAgo };

        public static readonly CreatePromotion Expired = new() { Code = "EXPIRED20", DiscountRate = 0.20m, IsActive = true, ValidFrom = LongAgo, ValidTo = LongAgo.AddYears(1) };

        public static readonly CreatePromotion Inactive = new() { Code = "INACTIVE30", DiscountRate = 0.30m, IsActive = false, ValidFrom = LongAgo };

        public const string UnknownCode = "NOSUCHCODE";
    }
}