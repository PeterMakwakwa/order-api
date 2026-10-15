using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Observability.Messages;
using Order.Infrastructure.Payments.Exceptions;

namespace Order.Infrastructure.Payments
{
    internal sealed class PaymentGatewayResolver(IEnumerable<IPaymentGateway> paymentGateways) : IPaymentGatewayResolver
    {
        public IPaymentGateway Resolve(PaymentMethods paymentMethod)
        {
            return paymentGateways.FirstOrDefault(gateway => gateway.Supports(paymentMethod))
                ?? throw new PaymentGatewayNotRegisteredException(string.Format(ErrorMessages.PaymentGatewayNotRegisteredErrorMessage, paymentMethod));
        }
    }
}