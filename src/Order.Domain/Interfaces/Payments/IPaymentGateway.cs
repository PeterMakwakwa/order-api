using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments.Models;

namespace Order.Domain.Interfaces.Payments
{
    public interface IPaymentGateway
    {
        string Name { get; }

        bool Supports(PaymentMethods paymentMethod);

        Task<PaymentResult> ChargeAsync(PaymentRequest paymentRequest, CancellationToken cancellationToken = default);
    }
}