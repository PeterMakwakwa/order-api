using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Infrastructure.Payments.Exceptions
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class PaymentGatewayConfigurationException : Exception
    {
        public PaymentGatewayConfigurationException()
        {
        }

        public PaymentGatewayConfigurationException(string? message) : base(message)
        {
        }

        public PaymentGatewayConfigurationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private PaymentGatewayConfigurationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}