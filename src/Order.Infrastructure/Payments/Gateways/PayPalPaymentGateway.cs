using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Observability.Interfaces;
using Order.Infrastructure.Payments.Gateways.Base;

namespace Order.Infrastructure.Payments.Gateways
{
    internal sealed class PayPalPaymentGateway(HttpClient httpClient, IObservabilityManager observabilityManager)
        : HttpPaymentGateway(httpClient, observabilityManager)
    {
        public override string Name => PaymentGatewayNames.PayPal;

        public override bool Supports(PaymentMethods paymentMethod)
        {
            return paymentMethod == PaymentMethods.PayPal;
        }
    }
}