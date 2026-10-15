using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// Maps to 404 Not Found.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class OrderNotFoundException : Exception
    {
        public OrderNotFoundException()
        {
        }

        public OrderNotFoundException(string? message) : base(message)
        {
        }

        public OrderNotFoundException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private OrderNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}