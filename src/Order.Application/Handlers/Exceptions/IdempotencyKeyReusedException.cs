using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// The Idempotency-Key was already used by this customer for a different request. Maps to 422 Unprocessable Entity.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class IdempotencyKeyReusedException : Exception
    {
        public IdempotencyKeyReusedException()
        {
        }

        public IdempotencyKeyReusedException(string? message) : base(message)
        {
        }

        public IdempotencyKeyReusedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private IdempotencyKeyReusedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}