using Order.Domain.Enums;

namespace Order.Domain.Interfaces.Payments
{
    /// <summary>
    /// Picks the gateway for a payment method, so adding a method doesn't change the order flow.
    /// </summary>
    public interface IPaymentGatewayResolver
    {
        IPaymentGateway Resolve(PaymentMethods paymentMethod);
    }
}