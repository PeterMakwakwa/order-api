namespace Order.Application.Constants
{
    public static class ResponseHeaderKeys
    {
        /// <summary>
        /// "true" when the response is the stored result of an earlier request with the same Idempotency-Key.
        /// </summary>
        public const string IdempotentReplayed = "Idempotent-Replayed";
    }
}