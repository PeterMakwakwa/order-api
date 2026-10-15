using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Domain.UnitOfWork.Exceptions
{
    /// <summary>
    /// A save broke a unique constraint (e.g. the same customer and idempotency key twice).
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class UniqueConstraintViolationException : Exception
    {
        public UniqueConstraintViolationException()
        {
        }

        public UniqueConstraintViolationException(string? message) : base(message)
        {
        }

        public UniqueConstraintViolationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private UniqueConstraintViolationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}