using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Domain.UnitOfWork.Exceptions
{
    /// <summary>
    /// A save found the row changed since it was loaded (optimistic concurrency), e.g. another processor claimed the same outbox message.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException()
        {
        }

        public ConcurrencyConflictException(string? message) : base(message)
        {
        }

        public ConcurrencyConflictException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private ConcurrencyConflictException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}