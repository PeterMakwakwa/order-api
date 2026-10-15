using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// A request with the same Idempotency-Key is still being processed. Maps to 409 Conflict; the client retries later with the same key.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class IdempotentRequestInProgressException : Exception
    {
        public IdempotentRequestInProgressException()
        {
        }

        public IdempotentRequestInProgressException(string? message) : base(message)
        {
        }

        public IdempotentRequestInProgressException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private IdempotentRequestInProgressException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}