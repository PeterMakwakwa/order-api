using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;

namespace Order.Infrastructure.Payments.Gateways
{
    /// <summary>
    /// Bank transfers need no provider call; the order waits for the transfer to arrive.
    /// </summary>
    internal sealed class BankTransferPaymentGateway : IPaymentGateway
    {
        public string Name => PaymentGatewayNames.BankTransfer;

        public bool Supports(PaymentMethods paymentMethod)
        {
            return paymentMethod == PaymentMethods.BankTransfer;
        }

        public Task<PaymentResult> ChargeAsync(PaymentRequest paymentRequest, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PaymentResult.Pending());
        }
    }
}