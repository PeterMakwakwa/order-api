using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class HandlerException : Exception
    {
        public HandlerException()
        {
        }

        public HandlerException(string? message) : base(message)
        {
        }

        public HandlerException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private HandlerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}