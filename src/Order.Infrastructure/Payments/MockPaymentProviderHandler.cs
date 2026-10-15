using System.Net;

namespace Order.Infrastructure.Payments
{
    /// <summary>
    /// Stands in for a payment provider when ApplicationConfiguration:PaymentGateways:{name}:UseMockProvider
    /// is true, so the API can be run locally without provider credentials. Every charge is accepted (202).
    /// </summary>
    internal sealed class MockPaymentProviderHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { RequestMessage = request });
        }
    }
}