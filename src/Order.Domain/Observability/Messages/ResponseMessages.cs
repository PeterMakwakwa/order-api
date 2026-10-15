namespace Order.Domain.Observability.Messages
{
    public static class ResponseMessages
    {
        public const string RequestValidationFailure = "The request payload has failed validation. {0}";
        public const string CustomerIdInvalid = "A valid customer Id must be provided, it must be greater than zero.";
        public const string CustomerEmailInvalid = "A valid customer email address must be provided.";
        public const string PaymentMethodInvalid = "A valid payment method must be provided (CreditCard, DebitCard, PayPal or BankTransfer).";
        public const string PromotionCodeEmpty = "The promotion code must not be empty when provided.";
        public const string PromotionCodeTooLong = "The promotion code must be at most {0} characters.";
        public const string PromotionCodeInvalid = "The promotion code {0} is not valid.";
        public const string ItemsInvalid = "At least one item must be provided.";
        public const string ItemsTooMany = "At most {0} items can be provided.";
        public const string ItemProductIdInvalid = "The item's product Id must be greater than zero.";
        public const string ItemProductNotFound = "The item's product Id {0} does not exist.";
        public const string ItemQuantityInvalid = "The item's quantity must be greater than zero.";
        public const string ItemQuantityTooLarge = "The quantity of each product, including repeats of the same product, must be at most {0}.";
        public const string OrderIdInvalid = "A valid order Id must be provided.";
        public const string CursorInvalid = "The cursor is not valid; use the nextCursor of a previous page.";
        public const string PageSizeInvalid = "The page size must be between 1 and {0}.";
        public const string OrderNotFound = "Order {0} was not found.";
        public const string OrderReferencesInvalid = "The order refers to products or a promotion that are not valid.";
        public const string IdempotencyKeyReused = "The Idempotency-Key has already been used for a different request.";
        public const string IdempotentRequestInProgress = "A request with this Idempotency-Key is still being processed. Retry later with the same key.";
        public const string HeaderRequired = "The {0} header is required.";
        public const string Unauthorized = "A valid access token is required.";
        public const string Forbidden = "You are not allowed to access this resource.";
        public const string TooManyRequests = "Too many requests. Retry after {0} seconds.";
    }
}