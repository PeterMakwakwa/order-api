namespace Order.Domain.Entities.Messages
{
    /// <summary>
    /// The messages of the domain rules the entities enforce (raised as DomainRuleException).
    /// </summary>
    public static class DomainErrorMessages
    {
        public const string OrderCustomerIdInvalidErrorMessage = "An order requires a customer id greater than zero.";
        public const string OrderCustomerEmailInvalidErrorMessage = "An order requires a customer email.";
        public const string OrderIdempotencyKeyRequiredErrorMessage = "An order requires an idempotency key and request hash.";
        public const string OrderPaymentMethodInvalidErrorMessage = "Payment method {0} is not supported.";
        public const string OrderDiscountRateInvalidErrorMessage = "The discount rate must be between 0 (inclusive) and 1 (exclusive).";
        public const string OrderDiscountWithoutPromotionErrorMessage = "A discount can only be applied through a promotion code.";
        public const string OrderLineItemsRequiredErrorMessage = "An order must contain at least one line item.";
        public const string OrderTooManyLineItemsErrorMessage = "An order can contain at most {0} line items.";
        public const string OrderAlreadySettledErrorMessage = "Order {0} is already settled ({1}).";
        public const string OrderPaymentOutcomeInvalidErrorMessage = "Payment outcome {0} is not supported.";
        public const string OrderPaymentNotRetryableErrorMessage = "The payment for order {0} cannot be retried ({1}, {2}); only a provider error can.";
        public const string LineItemQuantityInvalidErrorMessage = "A line item quantity must be between 1 and {0}.";
        public const string LineItemUnitPriceInvalidErrorMessage = "A line item unit price cannot be negative.";
        public const string ProductNameInvalidErrorMessage = "A product name is required.";
        public const string ProductPriceInvalidErrorMessage = "A product price cannot be negative.";
        public const string PromotionCodeInvalidErrorMessage = "A promotion code is required.";
        public const string PromotionDiscountRateInvalidErrorMessage = "A promotion discount rate must be greater than 0 and less than 1.";
        public const string PromotionValidityInvalidErrorMessage = "A promotion must end after it starts.";
    }
}