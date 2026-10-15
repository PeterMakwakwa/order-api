using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Order.Application.Constants;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Application.Models.Response.V1;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Domain = Order.Domain.Entities;

namespace Order.Tests.UnitTests.Application.Handlers
{
    public sealed class PlaceOrderRequestHandlerTests
    {
        private IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
        private ServiceProvider? _serviceProvider;
        private IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>? _handler;
        private string _correlationId = string.Empty;

        [SetUp]
        public async Task SetupAsync()
        {
            _paymentGateway = Substitute.For<IPaymentGateway>();
            _paymentGateway.Name.Returns(PaymentGatewayNames.Card);
            IPaymentGatewayResolver paymentGatewayResolver = Substitute.For<IPaymentGatewayResolver>();
            paymentGatewayResolver.Resolve(Arg.Any<PaymentMethods>()).Returns(_paymentGateway);

            _serviceProvider = UnitTestSetup.Setup(services => services.AddTransient(_ => paymentGatewayResolver));
            await DefaultDataSetup.AddDefaultDataAsync(_serviceProvider);

            _handler = _serviceProvider.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>();
            _correlationId = Guid.NewGuid().ToString();
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task HandleAsync_PaymentSucceeds_PersistsCompletedOrderAndOutboxMessage()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().Build();

            // Act
            PlaceOrderResponse response = await _handler!.HandleAsync(request, TestHeaders.Create(_correlationId));

            // Assert
            Domain::Order order = (await FindOrdersAsync()).Single(o => o.UniqueId == response.OrderId);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Id, Is.EqualTo(_correlationId));
                Assert.That(response.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(response.Subtotal, Is.EqualTo(PlaceOrderRequestBuilder.DefaultSubtotal));
                Assert.That(response.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(response.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
                Assert.That(response.IsReplay, Is.False);
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(order.CorrelationId, Is.EqualTo(_correlationId));
                Assert.That(order.DiscountRate, Is.EqualTo(DefaultPromotions.Welcome10.DiscountRate));
                Assert.That(order.LineItems, Has.Count.EqualTo(2));
                Assert.That(await FindOutboxMessagesAsync(), Has.Count.EqualTo(1));
            }

            await _paymentGateway.Received(1).ChargeAsync(
                Arg.Is<PaymentRequest>(p => p.OrderId == response.OrderId && p.Amount == PlaceOrderRequestBuilder.DefaultTotal),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HandleAsync_NoPromotionCode_ChargesFullPrice()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode(null).Build();

            // Act
            PlaceOrderResponse response = await _handler!.HandleAsync(request, TestHeaders.Create());

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultSubtotal));
                Assert.That(response.PromotionCode, Is.Null);
            }
        }

        [Test]
        public async Task HandleAsync_LowerCasePromotionCode_IsApplied()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode("welcome10").Build();

            // Act
            PlaceOrderResponse response = await _handler!.HandleAsync(request, TestHeaders.Create());

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(response.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
            }
        }

        [TestCase(DefaultPromotions.UnknownCode)]
        [TestCase("EXPIRED20")]
        [TestCase("INACTIVE30")]
        public async Task HandleAsync_PromotionNotApplicable_ThrowsValidationWithoutCharging(string promotionCode)
        {
            // Assemble
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode(promotionCode).Build();

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(request, TestHeaders.Create()));

            // Assert
            OrderValidationException validationException = (OrderValidationException)exception!.GetBaseException();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(validationException.Errors.Keys, Is.EquivalentTo(new[] { nameof(PlaceOrderRequest.PromotionCode) }));
                Assert.That(validationException.Errors[nameof(PlaceOrderRequest.PromotionCode)].Single(), Does.Contain(promotionCode));
                Assert.That(await FindOrdersAsync(), Is.Empty);
            }

            await _paymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_UnknownProductAndPromotion_ReportsBothWithoutCharging()
        {
            // Assemble
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.UnknownProductId, Quantity = 1 }])
                .WithPromotionCode(DefaultPromotions.UnknownCode)
                .Build();

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(request, TestHeaders.Create()));

            // Assert
            OrderValidationException validationException = (OrderValidationException)exception!.GetBaseException();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(validationException.Errors.Keys, Is.EquivalentTo(new[] { nameof(PlaceOrderRequest.Items), nameof(PlaceOrderRequest.PromotionCode) }));
                Assert.That(validationException.Errors[nameof(PlaceOrderRequest.Items)].Single(), Does.Contain(DefaultProducts.UnknownProductId.ToString()));
            }

            await _paymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_BankTransfer_ReturnsAwaitingPayment()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Pending());
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPaymentMethod(PaymentMethods.BankTransfer).Build();

            // Act
            PlaceOrderResponse response = await _handler!.HandleAsync(request, TestHeaders.Create());

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
                Assert.That(await FindOutboxMessagesAsync(), Has.Count.EqualTo(1));
            }
        }

        [TestCase(PaymentOutcome.Rejected, typeof(PaymentDeclinedException))]
        [TestCase(PaymentOutcome.Duplicate, typeof(DuplicatePaymentException))]
        [TestCase(PaymentOutcome.ProviderError, typeof(PaymentProviderUnavailableException))]
        public async Task HandleAsync_PaymentNotAccepted_RecordsFailedOrderAndThrows(PaymentOutcome outcome, Type expectedException)
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Failed(outcome, "Card declined"));
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().Build();

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(request, TestHeaders.Create()));

            // Assert
            Domain::Order order = (await FindOrdersAsync()).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.GetBaseException(), Is.TypeOf(expectedException));
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PaymentFailed));
                Assert.That(order.PaymentOutcome, Is.EqualTo(outcome));
                Assert.That(order.PaymentFailureReason, Is.EqualTo("Card declined"));
                Assert.That(await FindOutboxMessagesAsync(), Is.Empty);
            }
        }

        [Test]
        public async Task HandleAsync_SameIdempotencyKeyAndRequest_ReplaysWithoutChargingAgain()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().Build();
            string idempotencyKey = Guid.NewGuid().ToString();
            PlaceOrderResponse first = await _handler!.HandleAsync(request, TestHeaders.Create(_correlationId, idempotencyKey));

            // Act
            PlaceOrderResponse replay = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(replay.IsReplay, Is.True);
                Assert.That(replay with { IsReplay = false }, Is.EqualTo(first));
                Assert.That(await FindOrdersAsync(), Has.Count.EqualTo(1));
                Assert.That(await FindOutboxMessagesAsync(), Has.Count.EqualTo(1));
            }

            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_SameIdempotencyKeyForAnotherCustomer_PlacesSeparateOrder()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            PlaceOrderResponse first = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Act
            PlaceOrderResponse second = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().WithCustomerId(7).Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(second.IsReplay, Is.False);
                Assert.That(second.OrderId, Is.Not.EqualTo(first.OrderId));
            }
        }

        [Test]
        public async Task HandleAsync_DeclinedThenRetriedWithSameKey_ReplaysTheDeclineWithoutCharging()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Failed(PaymentOutcome.Rejected, "Card declined"));
            string idempotencyKey = Guid.NewGuid().ToString();
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() =>
                _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<PaymentDeclinedException>());
            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_SameIdempotencyKeyDifferentRequest_ThrowsKeyReused()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));
            PlaceOrderRequest differentRequest = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Monitor.Id, Quantity = 1 }])
                .Build();

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() =>
                _handler!.HandleAsync(differentRequest, TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<IdempotencyKeyReusedException>());
            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_SameIdempotencyKeyWhileFirstIsCharging_ThrowsInProgress()
        {
            // Assemble: while the first request is charging, the client retries with the same key.
            string idempotencyKey = Guid.NewGuid().ToString();
            Exception? retryException = null;
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    retryException = await CatchAsync(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));
                    return PaymentResult.Succeeded();
                });

            // Act
            PlaceOrderResponse first = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(first.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(retryException?.GetBaseException(), Is.TypeOf<IdempotentRequestInProgressException>());
            }

            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public void HandleAsync_MissingCorrelationId_ThrowsKeyNotFound()
        {
            // Assemble
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().Build();

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(request));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void HandleAsync_MissingIdempotencyKey_ThrowsKeyNotFound()
        {
            // Assemble
            Dictionary<string, string> headers = TestHeaders.Create();
            headers.Remove(RequestHeaderKeys.IdempotencyKey);

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), headers));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public async Task HandleAsync_PaymentSucceeds_RecordsCustomMetrics()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            using MetricCollector<long> successCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestSuccessCount.Name);
            using MetricCollector<long> amountCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.OrderTotalAmount.Name);
            using MetricCollector<long> chargeCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PaymentChargeCount.Name);

            // Act
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create());

            // Assert
            CollectedMeasurement<long> success = successCollector.GetMeasurementSnapshot().Single();
            CollectedMeasurement<long> charge = chargeCollector.GetMeasurementSnapshot().Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(success.Tags[MetricDimensions.PaymentMethod], Is.EqualTo(nameof(PaymentMethods.CreditCard)));
                Assert.That(success.Tags[MetricDimensions.OrderStatus], Is.EqualTo(nameof(OrderStatus.Completed)));
                Assert.That(amountCollector.GetMeasurementSnapshot().Single().Value, Is.EqualTo(8097));
                Assert.That(charge.Tags[MetricDimensions.PaymentGateway], Is.EqualTo(PaymentGatewayNames.Card));
                Assert.That(charge.Tags[MetricDimensions.PaymentOutcome], Is.EqualTo(nameof(PaymentOutcome.Succeeded)));
            }
        }

        [Test]
        public async Task HandleAsync_Replay_RecordsReplayedMetric()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));
            using MetricCollector<long> replayedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestReplayedCount.Name);

            // Act
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            Assert.That(replayedCollector.GetMeasurementSnapshot().Single().Tags[MetricDimensions.OrderStatus], Is.EqualTo(nameof(OrderStatus.Completed)));
        }

        [Test]
        public void HandleAsync_PaymentRejected_RecordsFailedMetricWithReason()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Failed(PaymentOutcome.Rejected, "Card declined"));
            using MetricCollector<long> failedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestFailedCount.Name);

            // Act
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create()));

            // Assert
            CollectedMeasurement<long> failed = failedCollector.GetMeasurementSnapshot().Single();
            Assert.That(failed.Tags[MetricDimensions.FailureReason], Is.EqualTo(nameof(PaymentDeclinedException)));
        }

        [Test]
        public async Task HandleAsync_CallerCancelsWhileCharging_StillRecordsTheOutcome()
        {
            // Assemble: the client disconnects while the provider is charging the card.
            using CancellationTokenSource callerCancellation = new();
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    callerCancellation.Cancel();
                    callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                    return PaymentResult.Succeeded();
                });

            // Act
            PlaceOrderResponse response = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(), callerCancellation.Token);

            // Assert
            Domain::Order order = (await FindOrdersAsync()).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(await FindOutboxMessagesAsync(), Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task HandleAsync_ProviderErrorThenRetriedWithSameKey_ChargesTheSameOrderAgain()
        {
            // Assemble
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(PaymentResult.Failed(PaymentOutcome.ProviderError, "Provider down"), PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            HandlerException? firstException = Assert.ThrowsAsync<HandlerException>(() =>
                _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Act
            PlaceOrderResponse retried = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            Domain::Order order = (await FindOrdersAsync()).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstException!.GetBaseException(), Is.TypeOf<PaymentProviderUnavailableException>());
                Assert.That(retried.IsReplay, Is.False);
                Assert.That(retried.OrderId, Is.EqualTo(order.UniqueId));
                Assert.That(retried.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(order.PaymentFailureReason, Is.Null);
                Assert.That(await FindOutboxMessagesAsync(), Has.Count.EqualTo(1));
            }

            // Both charges use the order id as the provider idempotency key, so the provider can never charge twice.
            await _paymentGateway.Received(2).ChargeAsync(Arg.Is<PaymentRequest>(p => p.OrderId == order.UniqueId), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HandleAsync_SuccessfulRetryAfterProviderError_IsThenReplayed()
        {
            // Assemble
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(PaymentResult.Failed(PaymentOutcome.ProviderError, "Provider down"), PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Act
            PlaceOrderResponse replay = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            Assert.That(replay.IsReplay, Is.True);
            await _paymentGateway.ReceivedWithAnyArgs(2).ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_RetryWhileAnotherRetryIsCharging_ThrowsInProgress()
        {
            // Assemble: the first charge fails with a provider error; while the retry is charging, the client retries again.
            string idempotencyKey = Guid.NewGuid().ToString();
            Exception? secondRetryException = null;
            int charges = 0;
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    if (++charges == 1)
                    {
                        return PaymentResult.Failed(PaymentOutcome.ProviderError, "Provider down");
                    }

                    secondRetryException = await CatchAsync(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));
                    return PaymentResult.Succeeded();
                });
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Act
            PlaceOrderResponse retried = await _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(retried.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(secondRetryException?.GetBaseException(), Is.TypeOf<IdempotentRequestInProgressException>());
                Assert.That(charges, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task SaveChangesAsync_TwoRetriesOfTheSameOrderAtOnce_OnlyOneSaves()
        {
            // Assemble: an order whose payment failed with a provider error, loaded by two requests at the same time.
            GivenPaymentResult(PaymentResult.Failed(PaymentOutcome.ProviderError, "Provider down"));
            string idempotencyKey = Guid.NewGuid().ToString();
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();
            using IOrderUnitOfWork first = unitOfWorkFactory.Create();
            using IOrderUnitOfWork second = unitOfWorkFactory.Create();
            Domain::Order firstCopy = (await first.OrderRepository.GetByIdempotencyKeyAsync(PlaceOrderRequestBuilder.DefaultCustomerId, idempotencyKey))!;
            Domain::Order secondCopy = (await second.OrderRepository.GetByIdempotencyKeyAsync(PlaceOrderRequestBuilder.DefaultCustomerId, idempotencyKey))!;
            firstCopy.RetryPayment(DateTimeOffset.UtcNow);
            secondCopy.RetryPayment(DateTimeOffset.UtcNow);
            await first.SaveChangesAsync();

            // Act & Assert
            Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
        }

        [Test]
        public void HandleAsync_UnknownProduct_RecordsRejectedInsteadOfFailedMetric()
        {
            // Assemble
            using MetricCollector<long> rejectedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestRejectedCount.Name);
            using MetricCollector<long> failedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestFailedCount.Name);
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.UnknownProductId, Quantity = 1 }])
                .Build();

            // Act
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(request, TestHeaders.Create()));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rejectedCollector.GetMeasurementSnapshot().Single().Tags[MetricDimensions.FailureReason], Is.EqualTo(nameof(OrderValidationException)));
                Assert.That(failedCollector.GetMeasurementSnapshot(), Is.Empty);
            }
        }

        [Test]
        public void HandleAsync_ReplayedDecline_IsNotCountedAsFailedAgain()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Failed(PaymentOutcome.Rejected, "Card declined"));
            string idempotencyKey = Guid.NewGuid().ToString();
            using MetricCollector<long> failedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestFailedCount.Name);
            using MetricCollector<long> replayedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.PlaceOrderRequestReplayedCount.Name);
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Act
            Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(failedCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(1));
                Assert.That(replayedCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task HandleAsync_SameOrderWithItemsReorderedSplitAndCodeInAnotherCase_IsReplayed()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            PlaceOrderResponse first = await _handler!.HandleAsync(new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = 1 }, new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 2 }])
                .WithPromotionCode("WELCOME10")
                .Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));
            PlaceOrderRequest sameOrder = new PlaceOrderRequestBuilder()
                .WithItems(
                [
                    new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 1 },
                    new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = 1 },
                    new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 1 }
                ])
                .WithPromotionCode(" welcome10 ")
                .Build();

            // Act
            PlaceOrderResponse retry = await _handler!.HandleAsync(sameOrder, TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(retry.IsReplay, Is.True);
                Assert.That(retry.OrderId, Is.EqualTo(first.OrderId));
            }

            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task HandleAsync_SameKeyWithDifferentQuantity_StillThrowsKeyReused()
        {
            // Assemble
            GivenPaymentResult(PaymentResult.Succeeded());
            string idempotencyKey = Guid.NewGuid().ToString();
            await _handler!.HandleAsync(new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 2 }])
                .Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => _handler!.HandleAsync(new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 3 }])
                .Build(), TestHeaders.Create(idempotencyKey: idempotencyKey)));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<IdempotencyKeyReusedException>());
        }

        private static async Task<Exception?> CatchAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private void GivenPaymentResult(PaymentResult paymentResult)
        {
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(paymentResult);
        }

        private async Task<List<Domain::Order>> FindOrdersAsync()
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                return [.. await unitOfWork.OrderRepository.GetAsync()];
            }
        }

        private async Task<List<OrderPlacedOutboxMessage>> FindOutboxMessagesAsync()
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                return [.. await unitOfWork.OrderPlacedOutboxRepository.GetAsync()];
            }
        }
    }
}