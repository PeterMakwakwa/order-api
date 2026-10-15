using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Infrastructure.Persistence.Exceptions
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class SaveContextException : Exception
    {
        public SaveContextException()
        {
        }

        public SaveContextException(string? message) : base(message)
        {
        }

        public SaveContextException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private SaveContextException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}