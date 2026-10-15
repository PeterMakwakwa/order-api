using Order.Domain.Commands;
using Order.Domain.Entities.Base;

namespace Order.Domain.Entities
{
    /// <summary>
    /// Row in the queueOrderPlaced outbox table, written in the same transaction as the order's payment outcome and
    /// processed by a background handler. Delivery is at-least-once.
    /// A processor first claims the message (<see cref="Claim"/>): the lease stops other instances picking it up, and
    /// <see cref="LockToken"/> is a concurrency token, so if two instances claim at the same time only one save succeeds.
    /// </summary>
    public sealed class OrderPlacedOutboxMessage : Entity<long>
    {
        private const int MaxErrorLength = 2000;

        /// <summary>
        /// For Entity Framework only.
        /// </summary>
        private OrderPlacedOutboxMessage()
            : base(0)
        {
            Version = 0;
            Message = string.Empty;
            CreatedDate = DateTimeOffset.MinValue;
        }

        public OrderPlacedOutboxMessage(CreateOrderPlacedOutboxMessage createOrderPlacedOutboxMessageCommand)
            : this()
        {
            Process(createOrderPlacedOutboxMessageCommand);
        }

        public int Version { get; private set; }

        public string Message { get; private set; }

        public DateTimeOffset CreatedDate { get; private set; }

        public DateTimeOffset? ProcessedDate { get; private set; }

        public int Attempts { get; private set; }

        public string? LastError { get; private set; }

        /// <summary>
        /// While in the future, another processor holds the message.
        /// </summary>
        public DateTimeOffset? LockedUntil { get; private set; }

        /// <summary>
        /// Changes on every claim; used as the optimistic concurrency token.
        /// </summary>
        public Guid? LockToken { get; private set; }

        public bool IsClaimableAt(DateTimeOffset moment, int maxAttempts)
        {
            return ProcessedDate == null && Attempts < maxAttempts && (LockedUntil == null || LockedUntil < moment);
        }

        public void Claim(DateTimeOffset lockedUntil)
        {
            LockedUntil = lockedUntil;
            LockToken = Guid.NewGuid();
        }

        public void MarkAsProcessed(DateTimeOffset processedDate)
        {
            ProcessedDate = processedDate;
            LastError = null;
            LockedUntil = null;
        }

        public void RecordFailedAttempt(string error)
        {
            Attempts++;
            LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
            LockedUntil = null;
        }

        private void Process(CreateOrderPlacedOutboxMessage createOrderPlacedOutboxMessageCommand)
        {
            Version = createOrderPlacedOutboxMessageCommand.Version;
            Message = createOrderPlacedOutboxMessageCommand.Message;
            CreatedDate = createOrderPlacedOutboxMessageCommand.CreatedDate;
        }
    }
}