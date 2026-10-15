using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Observability.Interfaces;
using Order.Infrastructure.Payments.Gateways.Base;

namespace Order.Infrastructure.Payments.Gateways
{
    internal sealed class CardPaymentGateway(HttpClient httpClient, IObservabilityManager observabilityManager)
        : HttpPaymentGateway(httpClient, observabilityManager)
    {
        public override string Name => PaymentGatewayNames.Card;

        public override bool Supports(PaymentMethods paymentMethod)
        {
            return paymentMethod is PaymentMethods.CreditCard or PaymentMethods.DebitCard;
        }
    }
}