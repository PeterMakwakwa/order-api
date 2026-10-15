using Order.Domain.Enums;

namespace Order.Domain.Interfaces.Payments.Models
{
    public sealed record PaymentResult
    {
        public required PaymentOutcome Outcome { get; init; }

        public string? Reason { get; init; }

        public bool IsAccepted => Outcome is PaymentOutcome.Succeeded or PaymentOutcome.Pending;

        public static PaymentResult Succeeded()
        {
            return new PaymentResult { Outcome = PaymentOutcome.Succeeded };
        }

        public static PaymentResult Pending()
        {
            return new PaymentResult { Outcome = PaymentOutcome.Pending };
        }

        public static PaymentResult Failed(PaymentOutcome outcome, string reason)
        {
            return new PaymentResult { Outcome = outcome, Reason = reason };
        }
    }
}