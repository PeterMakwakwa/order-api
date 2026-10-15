using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// The provider failed or could not be reached (5xx, timeout, open circuit). Maps to 503 Service Unavailable.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class PaymentProviderUnavailableException : Exception
    {
        public PaymentProviderUnavailableException()
        {
        }

        public PaymentProviderUnavailableException(string? message) : base(message)
        {
        }

        public PaymentProviderUnavailableException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private PaymentProviderUnavailableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}