using Order.Domain.Commands;
using Order.Domain.Entities.Base;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Entities.Messages;

namespace Order.Domain.Entities
{
    /// <summary>
    /// A promotion code and the discount it grants. Discounts only ever come from here, never from the client.
    /// </summary>
    public sealed class Promotion : Aggregate<int>
    {
        /// <summary>
        /// For Entity Framework only.
        /// </summary>
        private Promotion()
            : base(0)
        {
            Code = string.Empty;
            DiscountRate = 0;
            IsActive = false;
            ValidFrom = DateTimeOffset.MinValue;
        }

        public Promotion(CreatePromotion createPromotionCommand)
            : this()
        {
            Process(createPromotionCommand);
        }

        public string Code { get; private set; }

        public decimal DiscountRate { get; private set; }

        public bool IsActive { get; private set; }

        public DateTimeOffset ValidFrom { get; private set; }

        /// <summary>
        /// Exclusive end of the promotion; null for no end date.
        /// </summary>
        public DateTimeOffset? ValidTo { get; private set; }

        /// <summary>
        /// Codes are stored and looked up upper-case, so they are case-insensitive on every database.
        /// </summary>
        public static string Normalize(string code)
        {
            return code.Trim().ToUpperInvariant();
        }

        public bool IsApplicableAt(DateTimeOffset moment)
        {
            return IsActive && moment >= ValidFrom && (ValidTo == null || moment < ValidTo);
        }

        private void Process(CreatePromotion createPromotionCommand)
        {
            if (string.IsNullOrWhiteSpace(createPromotionCommand.Code))
            {
                throw new DomainRuleException(DomainErrorMessages.PromotionCodeInvalidErrorMessage);
            }

            if (createPromotionCommand.DiscountRate <= 0 || createPromotionCommand.DiscountRate >= 1)
            {
                throw new DomainRuleException(DomainErrorMessages.PromotionDiscountRateInvalidErrorMessage);
            }

            if (createPromotionCommand.ValidTo <= createPromotionCommand.ValidFrom)
            {
                throw new DomainRuleException(DomainErrorMessages.PromotionValidityInvalidErrorMessage);
            }

            Code = Normalize(createPromotionCommand.Code);
            DiscountRate = createPromotionCommand.DiscountRate;
            IsActive = createPromotionCommand.IsActive;
            ValidFrom = createPromotionCommand.ValidFrom;
            ValidTo = createPromotionCommand.ValidTo;
        }
    }
}