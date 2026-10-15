using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Application.Events.Exceptions
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class OutboxMessageDeserializationException : Exception
    {
        public OutboxMessageDeserializationException()
        {
        }

        public OutboxMessageDeserializationException(string? message) : base(message)
        {
        }

        public OutboxMessageDeserializationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private OutboxMessageDeserializationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}