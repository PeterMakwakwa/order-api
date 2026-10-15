namespace Order.Domain.Interfaces.Email.Models
{
    public sealed record EmailMessage
    {
        public required string To { get; init; }

        public required string Subject { get; init; }

        public required string Body { get; init; }

        /// <summary>
        /// Identifies this email across re-sends: the same key always produces the same Message-ID header, so mail servers
        /// and clients that de-duplicate on it show an email the outbox had to send twice only once. Null for a new id per send.
        /// </summary>
        public string? DeduplicationKey { get; init; }
    }
}