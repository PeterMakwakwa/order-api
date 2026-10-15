using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net;
using System.Net.Http.Json;

namespace Order.Infrastructure.Payments.Gateways.Base
{
    /// <summary>
    /// Shared behaviour for HTTP payment providers. The typed HttpClient carries the base address, API key and
    /// Polly pipeline (timeout, retry, circuit breaker); the amount travels in the body and the order id is the
    /// idempotency key, which makes retries safe.
    /// </summary>
    internal abstract class HttpPaymentGateway(HttpClient httpClient, IObservabilityManager observabilityManager) : IPaymentGateway
    {
        private const string ChargePath = "charge";
        private const int MaxReasonLength = 500;

        public abstract string Name { get; }

        public abstract bool Supports(PaymentMethods paymentMethod);

        public async Task<PaymentResult> ChargeAsync(PaymentRequest paymentRequest, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            using (HttpRequestMessage requestMessage = BuildChargeRequest(paymentRequest))
            {
                try
                {
                    using (HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, cancellationToken))
                    {
                        return await MapResponseAsync(responseMessage, cancellationToken);
                    }
                }
                catch (Exception ex) when (IsProviderUnreachable(ex, cancellationToken))
                {
                    string reason = string.Format(WarningMessages.PaymentProviderUnreachable, Name, paymentRequest.OrderId);
                    observabilityManager.LogMessage(reason).AsWarning(ex);

                    return PaymentResult.Failed(PaymentOutcome.ProviderError, reason);
                }
            }
        }

        private static HttpRequestMessage BuildChargeRequest(PaymentRequest paymentRequest)
        {
            HttpRequestMessage requestMessage = new(HttpMethod.Post, ChargePath)
            {
                Content = JsonContent.Create(new
                {
                    paymentRequest.OrderId,
                    paymentRequest.CustomerId,
                    paymentRequest.Amount,
                    paymentRequest.Currency,
                    PaymentMethod = paymentRequest.PaymentMethod.ToString()
                })
            };
            requestMessage.Headers.Add(RequestHeaderKeys.IdempotencyKey, paymentRequest.OrderId.ToString());

            return requestMessage;
        }

        /// <summary>
        /// Provider contract: 202 = success, 409 = duplicate order, 400 = bad request, 500 = server error.
        /// </summary>
        private static async Task<PaymentResult> MapResponseAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return responseMessage.StatusCode switch
            {
                HttpStatusCode.Accepted => PaymentResult.Succeeded(),
                HttpStatusCode.Conflict => PaymentResult.Failed(PaymentOutcome.Duplicate, await ReadReasonAsync(responseMessage, cancellationToken)),
                HttpStatusCode.BadRequest => PaymentResult.Failed(PaymentOutcome.Rejected, await ReadReasonAsync(responseMessage, cancellationToken)),
                _ => PaymentResult.Failed(PaymentOutcome.ProviderError, $"Unexpected status code {(int)responseMessage.StatusCode}.")
            };
        }

        private static async Task<string> ReadReasonAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            string body = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return responseMessage.StatusCode.ToString();
            }

            return body.Length > MaxReasonLength ? body[..MaxReasonLength] : body;
        }

        private static bool IsProviderUnreachable(Exception exception, CancellationToken cancellationToken)
        {
            return exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);
        }
    }
}