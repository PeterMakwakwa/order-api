using Order.Application.Constants;

namespace Order.Tests.Setup
{
    public static class TestHeaders
    {
        /// <summary>
        /// The headers the controller passes to the place-order handler; a new idempotency key unless one is given.
        /// </summary>
        public static Dictionary<string, string> Create(string? correlationId = null, string? idempotencyKey = null)
        {
            return new Dictionary<string, string>
            {
                { RequestHeaderKeys.CorrelationId, correlationId ?? Guid.NewGuid().ToString() },
                { RequestHeaderKeys.IdempotencyKey, idempotencyKey ?? Guid.NewGuid().ToString() }
            };
        }
    }
}