using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Order.Application.Handlers.Reconciliation;
using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Domain = Order.Domain.Entities;

namespace Order.Tests.UnitTests.Application.Handlers
{
    public sealed class StalledPaymentReconciliationHandlerTests
    {
        // Default ApplicationConfiguration:Reconciliation:StalledPaymentTimeout is 5 minutes.
        private static readonly TimeSpan LongAgo = TimeSpan.FromHours(1);

        private IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
        private ServiceProvider? _serviceProvider;
        private StalledPaymentReconciliationHandler? _handler;

        [SetUp]
        public async Task SetupAsync()
        {
            _paymentGateway = Substitute.For<IPaymentGateway>();
            _paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Succeeded());
            IPaymentGatewayResolver paymentGatewayResolver = Substitute.For<IPaymentGatewayResolver>();
            paymentGatewayResolver.Resolve(Arg.Any<PaymentMethods>()).Returns(_paymentGateway);

            _serviceProvider = UnitTestSetup.Setup(services => services.AddTransient(_ => paymentGatewayResolver));
            await DefaultDataSetup.AddDefaultDataAsync(_serviceProvider);

            _handler = ActivatorUtilities.CreateInstance<StalledPaymentReconciliationHandler>(_serviceProvider);
        }

        [TearDown]
        public void Teardown()
        {
            _handler?.Dispose();
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task ReconcileStalledPaymentsAsync_OrderLeftPendingPayment_ChargesItAgainWithTheSameKeyAndSettlesIt()
        {
            // Assemble: the process died between charging and recording the outcome an hour ago.
            Domain::Order stalled = await AddPendingOrderAsync(DateTimeOffset.UtcNow - LongAgo);
            using MetricCollector<long> reconciledCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PaymentReconciledCount.Name);

            // Act
            await _handler!.ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert
            Domain::Order order = await GetOrderAsync(stalled.UniqueId);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(await CountOutboxMessagesAsync(), Is.EqualTo(1));
                Assert.That(reconciledCollector.GetMeasurementSnapshot().Single().Tags[MetricDimensions.OrderStatus], Is.EqualTo(nameof(OrderStatus.Completed)));
            }

            await _paymentGateway.Received(1).ChargeAsync(Arg.Is<PaymentRequest>(p => p.OrderId == stalled.UniqueId && p.Amount == stalled.Total), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReconcileStalledPaymentsAsync_ProviderRejects_RecordsTheFailureWithoutConfirmation()
        {
            // Assemble
            _paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Failed(PaymentOutcome.Rejected, "Card declined"));
            Domain::Order stalled = await AddPendingOrderAsync(DateTimeOffset.UtcNow - LongAgo);

            // Act
            await _handler!.ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert
            Domain::Order order = await GetOrderAsync(stalled.UniqueId);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PaymentFailed));
                Assert.That(order.PaymentFailureReason, Is.EqualTo("Card declined"));
                Assert.That(await CountOutboxMessagesAsync(), Is.Zero);
            }
        }

        [Test]
        public async Task ReconcileStalledPaymentsAsync_OrderStillBeingCharged_IsLeftAlone()
        {
            // Assemble: placed a moment ago, so its own request may still be charging it.
            Domain::Order recent = await AddPendingOrderAsync(DateTimeOffset.UtcNow);

            // Act
            await _handler!.ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert
            Assert.That((await GetOrderAsync(recent.UniqueId)).Status, Is.EqualTo(OrderStatus.PendingPayment));
            await _paymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task ReconcileStalledPaymentsAsync_SettledOrders_AreLeftAlone()
        {
            // Assemble: old orders that were settled, including a provider error the client can retry itself.
            await AddPendingOrderAsync(DateTimeOffset.UtcNow - LongAgo, PaymentOutcome.Succeeded);
            await AddPendingOrderAsync(DateTimeOffset.UtcNow - LongAgo, PaymentOutcome.ProviderError);

            // Act
            await _handler!.ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert
            await _paymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task ReconcileStalledPaymentsAsync_OrderSettledElsewhereWhileCharging_KeepsThatOutcome()
        {
            // Assemble: while this processor charges, another instance settles the same order first.
            Domain::Order stalled = await AddPendingOrderAsync(DateTimeOffset.UtcNow - LongAgo);
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    using (IOrderUnitOfWork other = UnitOfWorkFactory().Create())
                    {
                        Domain::Order otherCopy = (await other.OrderRepository.GetByUniqueIdAsync(stalled.UniqueId))!;
                        otherCopy.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.Rejected, FailureReason = "Settled elsewhere" });
                        await other.SaveChangesAsync();
                    }

                    return PaymentResult.Succeeded();
                });

            // Act
            await _handler!.ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert: the in-memory database has no transactions, so that the outbox row is rolled back with the order is
            // checked against SQL Server (SqlServerIntegrationTests).
            Domain::Order order = await GetOrderAsync(stalled.UniqueId);
            Assert.That(order.PaymentFailureReason, Is.EqualTo("Settled elsewhere"));
        }

        private async Task<Domain::Order> AddPendingOrderAsync(DateTimeOffset createdDate, PaymentOutcome? settledWith = null)
        {
            Domain::Order order = new(new CreateOrderCommandBuilder().WithCreatedDate(createdDate).Build());
            if (settledWith != null)
            {
                order.Process(new RecordPaymentOutcome { Outcome = settledWith.Value });
            }

            using (IOrderUnitOfWork unitOfWork = UnitOfWorkFactory().Create())
            {
                await unitOfWork.OrderRepository.AddAsync(order);
                await unitOfWork.SaveChangesAsync();
            }

            return order;
        }

        private async Task<Domain::Order> GetOrderAsync(Guid orderId)
        {
            using (IOrderUnitOfWork unitOfWork = UnitOfWorkFactory().Create())
            {
                return (await unitOfWork.OrderRepository.GetByUniqueIdAsync(orderId))!;
            }
        }

        private async Task<int> CountOutboxMessagesAsync()
        {
            using (IOrderUnitOfWork unitOfWork = UnitOfWorkFactory().Create())
            {
                return (await unitOfWork.OrderPlacedOutboxRepository.GetAsync()).Count();
            }
        }

        private IUnitOfWorkFactory<IOrderUnitOfWork> UnitOfWorkFactory()
        {
            return _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();
        }
    }
}