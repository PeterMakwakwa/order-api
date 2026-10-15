using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Infrastructure.Email.Exceptions
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class EmailDeliveryException : Exception
    {
        public EmailDeliveryException()
        {
        }

        public EmailDeliveryException(string? message) : base(message)
        {
        }

        public EmailDeliveryException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private EmailDeliveryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}