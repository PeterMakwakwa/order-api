namespace Order.Domain.Enums
{
    public enum OrderStatus
    {
        PendingPayment,
        AwaitingPayment,
        Completed,
        PaymentFailed
    }
}