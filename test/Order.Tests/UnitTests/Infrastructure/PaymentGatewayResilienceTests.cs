using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Order.Application.Constants;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Metrics;
using Order.Tests.Fakes;
using Order.Tests.Setup;
using System.Net;

namespace Order.Tests.UnitTests.Infrastructure
{
    /// <summary>
    /// Runs the real resilient-client registration, with only the network stubbed.
    /// appsettings.UnitTest.json (Card): 2 retries, no back-off, circuit opens after 5 consecutive failures.
    /// </summary>
    public sealed class PaymentGatewayResilienceTests
    {
        private static readonly PaymentRequest PaymentRequest = new()
        {
            OrderId = Guid.NewGuid(),
            CustomerId = 42,
            Amount = 10m,
            Currency = "USD",
            PaymentMethod = PaymentMethods.CreditCard
        };

        private ServiceProvider? _serviceProvider;

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task ChargeAsync_TransientServerErrors_RetriesUntilSuccess()
        {
            // Assemble
            StubHttpMessageHandler handler = new(HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable, HttpStatusCode.Accepted);
            IPaymentGateway gateway = CreateCardGateway(handler);
            using MetricCollector<long> retryCollector = CreateCollector(MetricDefinitions.PaymentGatewayRetryCount.Name);

            // Act
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Outcome, Is.EqualTo(PaymentOutcome.Succeeded));
                Assert.That(handler.Requests, Has.Count.EqualTo(3));
                Assert.That(retryCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(2));
                Assert.That(retryCollector.GetMeasurementSnapshot()[0].Tags[MetricDimensions.PaymentGateway], Is.EqualTo(PaymentGatewayNames.Card));
            }
        }

        [Test]
        public async Task ChargeAsync_PersistentServerErrors_GivesUpAfterMaxRetries()
        {
            // Assemble
            StubHttpMessageHandler handler = new(HttpStatusCode.InternalServerError);
            IPaymentGateway gateway = CreateCardGateway(handler);

            // Act
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Outcome, Is.EqualTo(PaymentOutcome.ProviderError));
                Assert.That(handler.Requests, Has.Count.EqualTo(3));
            }
        }

        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.Conflict)]
        public async Task ChargeAsync_BusinessRejection_IsNotRetried(HttpStatusCode statusCode)
        {
            // Assemble
            StubHttpMessageHandler handler = new(statusCode);
            IPaymentGateway gateway = CreateCardGateway(handler);

            // Act
            await gateway.ChargeAsync(PaymentRequest);

            // Assert
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ChargeAsync_RepeatedFailures_OpensCircuitAndStopsCallingTheProvider()
        {
            // Assemble
            StubHttpMessageHandler handler = new(HttpStatusCode.InternalServerError);
            IPaymentGateway gateway = CreateCardGateway(handler);
            using MetricCollector<long> circuitCollector = CreateCollector(MetricDefinitions.PaymentGatewayCircuitOpenedCount.Name);

            // Act: 3 attempts, then 2 more failures open the circuit (5 in a row); the next call is short-circuited.
            await gateway.ChargeAsync(PaymentRequest);
            await gateway.ChargeAsync(PaymentRequest);
            int requestsWhenOpened = handler.Requests.Count;
            PaymentResult result = await gateway.ChargeAsync(PaymentRequest);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(requestsWhenOpened, Is.EqualTo(5));
                Assert.That(handler.Requests, Has.Count.EqualTo(5));
                Assert.That(result.Outcome, Is.EqualTo(PaymentOutcome.ProviderError));
                Assert.That(circuitCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(1));
            }
        }

        private IPaymentGateway CreateCardGateway(StubHttpMessageHandler handler)
        {
            _serviceProvider = UnitTestSetup.Setup(services => services
                .AddHttpClient(PaymentGatewayNames.Card)
                .ConfigurePrimaryHttpMessageHandler(() => handler));

            return _serviceProvider
                .GetRequiredService<IPaymentGatewayResolver>()
                .Resolve(PaymentMethods.CreditCard);
        }

        private MetricCollector<long> CreateCollector(string instrumentName)
        {
            return new MetricCollector<long>(meterScope: null, UnitTestSetup.ApplicationName, instrumentName);
        }
    }
}