using NSubstitute;
using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Interfaces;
using Order.Infrastructure.Payments.Gateways;
using Order.Tests.Fakes;
using System.Net;

namespace Order.Tests.UnitTests.Infrastructure
{
    public sealed class HttpPaymentGatewayTests
    {
        private static readonly PaymentRequest PaymentRequest = new()
        {
            OrderId = Guid.NewGuid(),
            CustomerId = 42,
            Amount = 80.97m,
            Currency = "USD",
            PaymentMethod = PaymentMethods.CreditCard
        };

        [TestCase(HttpStatusCode.Accepted, PaymentOutcome.Succeeded)]
        [TestCase(HttpStatusCode.Conflict, PaymentOutcome.Duplicate)]
        [TestCase(HttpStatusCode.BadRequest, PaymentOutcome.Rejected)]
        [TestCase(HttpStatusCode.InternalServerError, PaymentOutcome.ProviderError)]
        public async Task ChargeAsync_ProviderStatusCode_MapsToOutcome(HttpStatusCode statusCode, PaymentOutcome expectedOutcome)
        {
            // Assemble
            CardPaymentGateway gateway = CreateGateway(new StubHttpMessageHandler(statusCode));

            // Act
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        }

        [Test]
        public async Task ChargeAsync_Rejected_UsesProviderBodyAsReason()
        {
            // Assemble
            CardPaymentGateway gateway = CreateGateway(new StubHttpMessageHandler(HttpStatusCode.BadRequest));

            // Act
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            Assert.That(result.Reason, Is.EqualTo("Card declined"));
        }

        [Test]
        public async Task ChargeAsync_Always_PostsToChargeWithIdempotencyKey()
        {
            // Assemble
            StubHttpMessageHandler handler = new(HttpStatusCode.Accepted);
            CardPaymentGateway gateway = CreateGateway(handler);

            // Act
            await gateway.ChargeAsync(PaymentRequest);

            // Assert
            HttpRequestMessage request = handler.Requests.Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.ToString(), Is.EqualTo("https://card.test/charge"));
                Assert.That(request.Headers.GetValues(RequestHeaderKeys.IdempotencyKey).Single(), Is.EqualTo(PaymentRequest.OrderId.ToString()));
            }
        }

        [Test]
        public async Task ChargeAsync_ProviderUnreachable_ReturnsProviderError()
        {
            // Assemble
            CardPaymentGateway gateway = CreateGateway(new StubHttpMessageHandler(HttpStatusCode.Accepted)
            {
                ExceptionToThrow = new HttpRequestException("Connection refused")
            });

            // Act
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            Assert.That(result.Outcome, Is.EqualTo(PaymentOutcome.ProviderError));
        }

        [TestCase(PaymentMethods.CreditCard, true)]
        [TestCase(PaymentMethods.DebitCard, true)]
        [TestCase(PaymentMethods.PayPal, false)]
        [TestCase(PaymentMethods.BankTransfer, false)]
        public void Supports_PaymentMethod_OnlyCards(PaymentMethods paymentMethod, bool expected)
        {
            CardPaymentGateway gateway = CreateGateway(new StubHttpMessageHandler(HttpStatusCode.Accepted));

            Assert.That(gateway.Supports(paymentMethod), Is.EqualTo(expected));
        }

        private static CardPaymentGateway CreateGateway(StubHttpMessageHandler handler)
        {
            HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://card.test/") };

            return new CardPaymentGateway(httpClient, Substitute.For<IObservabilityManager>());
        }
    }
}