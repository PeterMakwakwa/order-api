using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// The provider rejected the payment (400 from the provider). Maps to 402 Payment Required.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class PaymentDeclinedException : Exception
    {
        public PaymentDeclinedException()
        {
        }

        public PaymentDeclinedException(string? message) : base(message)
        {
        }

        public PaymentDeclinedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private PaymentDeclinedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}