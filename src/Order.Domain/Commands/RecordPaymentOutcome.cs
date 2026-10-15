using Order.Domain.Enums;

namespace Order.Domain.Commands
{
    public sealed record RecordPaymentOutcome
    {
        public required PaymentOutcome Outcome { get; init; }

        public string? FailureReason { get; init; }
    }
}