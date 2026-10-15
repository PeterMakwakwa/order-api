using System.Diagnostics.CodeAnalysis;

namespace Order.Application.Handlers.Exceptions
{
    /// <summary>
    /// The request is well-formed but refers to things that do not exist or do not apply (unknown products,
    /// an invalid promotion code). Carries the errors per field, like a failed request validation.
    /// Maps to 400 Bad Request with validation problem details.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class OrderValidationException : Exception
    {
        public OrderValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
            : base(message)
        {
            Errors = errors;
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}