using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Order.Domain.Entities.Exceptions
{
    /// <summary>
    /// Thrown when an aggregate invariant would be broken. Requests are validated before they
    /// reach the domain, so this signals a programming error rather than a user mistake.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public sealed class DomainRuleException : Exception
    {
        public DomainRuleException()
        {
        }

        public DomainRuleException(string? message) : base(message)
        {
        }

        public DomainRuleException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        [Obsolete(DiagnosticId = "SYSLIB0051")]
        [SuppressMessage("Major Code Smell", "S1133:Do not forget to remove this deprecated code someday",
            Justification = "This is required based on serializable")]
        private DomainRuleException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}