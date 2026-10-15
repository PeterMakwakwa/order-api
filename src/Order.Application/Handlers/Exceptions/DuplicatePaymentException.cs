using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// The provider reported a duplicate order (409 from the provider). Maps to 409 Conflict.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class DuplicatePaymentException : Exception
    {
        public DuplicatePaymentException()
        {
        }

        public DuplicatePaymentException(string? message) : base(message)
        {
        }

        public DuplicatePaymentException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private DuplicatePaymentException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}