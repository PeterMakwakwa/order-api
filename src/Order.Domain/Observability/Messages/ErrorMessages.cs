namespace Order.Domain.Observability.Messages
{
    public static class ErrorMessages
    {
        public const string HandlerGenericErrorMessage = "An error occurred while processing the request in the handler.";
        public const string UnhandledExceptionErrorMessage = "An unexpected error occurred.";
        public const string SaveContextErrorMessage = "An error occurred while saving the database context.";
        public const string UniqueConstraintViolationErrorMessage = "The save broke a unique constraint.";
        public const string ConcurrencyConflictErrorMessage = "The row was changed by someone else since it was loaded.";
        public const string DatabaseConfigurationMissingErrorMessage = "The {0} database configuration is missing.";
        public const string DatabasePasswordMissingErrorMessage = "The {0} password is not configured. Set ApplicationConfiguration:Persistence:{0}:Password with dotnet user-secrets (Localhost) or an environment variable.";
        public const string DictionaryKeyNotFoundErrorMessage = "The required key, {0}, was not found in the dictionary.";
        public const string OutboxMessageDeserializationErrorMessage = "Something went wrong while trying to deserialize the outbox message.";
        public const string OutboxProcessingErrorMessage = "Something went wrong while processing the outbox.";
        public const string PaymentReconciliationErrorMessage = "Something went wrong while reconciling the payment of order {0}; it is retried on the next poll.";
        public const string PaymentReconciliationProcessingErrorMessage = "Something went wrong while reconciling stalled payments.";
        public const string EmailDeliveryErrorMessage = "Something went wrong while sending the email.";

        public const string PaymentDeclinedErrorMessage = "The payment for order {0} was declined: {1}";
        public const string DuplicatePaymentErrorMessage = "The payment provider reported order {0} as a duplicate.";
        public const string PaymentProviderUnavailableErrorMessage = "The payment provider is unavailable for order {0}. Please try again later.";
        public const string PaymentGatewayNotRegisteredErrorMessage = "No payment gateway is registered for {0}.";
        public const string PaymentGatewayConfigurationMissingErrorMessage = "The configuration for the {0} payment gateway is missing.";
    }
}