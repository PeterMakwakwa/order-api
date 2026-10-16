using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Order.Application.Events.V1.Produced;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Outbox;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using Order.Tests.Builders;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Application.Handlers
{
    public sealed class OrderPlacedOutboxMessageHandlerTests
    {
        private IEmailSender _emailSender = Substitute.For<IEmailSender>();

        private ServiceProvider? _serviceProvider;
        private OrderPlacedOutboxMessageHandler? _handler;

        [SetUp]
        public async Task SetupAsync()
        {
            _emailSender = Substitute.For<IEmailSender>();
            IPaymentGateway paymentGateway = Substitute.For<IPaymentGateway>();
            paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Pending());
            IPaymentGatewayResolver paymentGatewayResolver = Substitute.For<IPaymentGatewayResolver>();
            paymentGatewayResolver.Resolve(Arg.Any<PaymentMethods>()).Returns(paymentGateway);

            _serviceProvider = UnitTestSetup.Setup(services => services
                .AddTransient(_ => paymentGatewayResolver)
                .AddTransient(_ => _emailSender));
            await DefaultDataSetup.AddDefaultDataAsync(_serviceProvider);

            _handler = ActivatorUtilities.CreateInstance<OrderPlacedOutboxMessageHandler>(_serviceProvider);
        }

        [TearDown]
        public void Teardown()
        {
            _handler?.Dispose();
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_OrderPlaced_SendsConfirmationAndMarksProcessed()
        {
            // Assemble
            PlaceOrderResponse placed = await PlaceBankTransferOrderAsync();

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            Assert.That(message.ProcessedDate, Is.Not.Null);
            await _emailSender.Received(1).SendAsync(
                Arg.Is<EmailMessage>(email => email.To == "customer@example.com"
                    && email.Body.Contains(placed.OrderId.ToString())
                    && email.Body.Contains("awaiting your bank transfer")
                    && email.Body.Contains("- 1 x Keyboard at $49.99 each: $49.99")
                    && email.Body.Contains("- 2 x Mouse at $19.99 each: $39.98")
                    && email.Body.Contains("Discount (WELCOME10): -$9.00")
                    && email.Body.Contains("Total: $80.97")
                    && email.DeduplicationKey == $"order-confirmation.{placed.OrderId:N}"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_EmailFails_KeepsMessageForRetry()
        {
            // Assemble
            await PlaceBankTransferOrderAsync();
            _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("SMTP down"));

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(message.ProcessedDate, Is.Null);
                Assert.That(message.Attempts, Is.EqualTo(1));
                Assert.That(message.LastError, Does.Contain("SMTP down"));
            }
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_MaxAttemptsReached_StopsRetrying()
        {
            // Assemble
            await PlaceBankTransferOrderAsync();
            _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("SMTP down"));

            // Act (appsettings.UnitTest.json: MaxAttempts = 3)
            for (int run = 0; run < 5; run++)
            {
                await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);
            }

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            Assert.That(message.Attempts, Is.EqualTo(3));
            await _emailSender.Received(3).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_UnknownVersion_RecordsDeserializationFailure()
        {
            // Assemble
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                await unitOfWork.OrderPlacedOutboxRepository.AddAsync(new OrderPlacedOutboxMessage(new CreateOrderPlacedOutboxMessage
                {
                    Version = 99,
                    Message = "{}",
                    CreatedDate = DateTimeOffset.UtcNow
                }));
                await unitOfWork.SaveChangesAsync();
            }

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            Assert.That(message.LastError, Does.Contain("OutboxMessageDeserializationException"));
            await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_Version1MessageWithoutItems_StillSendsWithTheTotal()
        {
            // Assemble: a message written before version 2 added the line items.
            Guid orderId = Guid.NewGuid();
            await AddOutboxMessageAsync(1, $$"""
                {"Id":"{{orderId}}","CorrelationId":"c","CustomerId":42,"CustomerEmail":"customer@example.com","Total":80.97,"PaymentMethod":0,"Status":2,"CreatedDate":"2026-01-01T00:00:00+00:00"}
                """);

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.That((await GetOnlyOutboxMessageAsync()).ProcessedDate, Is.Not.Null);
            await _emailSender.Received(1).SendAsync(
                Arg.Is<EmailMessage>(email => email.Body == $"Your order {orderId} has been placed.{Environment.NewLine}Total: $80.97"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_NewerVersionThanSupported_RecordsDeserializationFailure()
        {
            // Assemble
            await AddOutboxMessageAsync(OrderPlaced.Version + 1, "{}");

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.That((await GetOnlyOutboxMessageAsync()).LastError, Does.Contain("OutboxMessageDeserializationException"));
            await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
        }

        [Test]
        public void BuildConfirmationEmail_OrderWithDiscount_ListsItemsSubtotalDiscountAndTotal()
        {
            // Assemble
            Guid orderId = Guid.NewGuid();
            OrderPlaced orderPlaced = new()
            {
                Id = orderId,
                CorrelationId = "c",
                CustomerId = 42,
                CustomerEmail = "customer@example.com",
                Subtotal = 89.97m,
                Total = 80.97m,
                PromotionCode = "WELCOME10",
                PaymentMethod = PaymentMethods.CreditCard,
                Status = OrderStatus.Completed,
                CreatedDate = DateTimeOffset.UtcNow,
                Items =
                [
                    new OrderPlacedLineItem { ProductId = 1, ProductName = "Keyboard", UnitPrice = 49.99m, Quantity = 1 },
                    new OrderPlacedLineItem { ProductId = 2, ProductName = "Mouse", UnitPrice = 19.99m, Quantity = 2 }
                ]
            };

            // Act
            EmailMessage email = OrderPlacedOutboxMessageHandler.BuildConfirmationEmail(orderPlaced);

            // Assert
            string[] expected =
            [
                $"Your order {orderId} has been placed.",
                "",
                "Items:",
                "- 1 x Keyboard at $49.99 each: $49.99",
                "- 2 x Mouse at $19.99 each: $39.98",
                "",
                "Subtotal: $89.97",
                "Discount (WELCOME10): -$9.00",
                "Total: $80.97"
            ];
            Assert.That(email.Body, Is.EqualTo(string.Join(Environment.NewLine, expected)));
        }

        [Test]
        public void BuildConfirmationEmail_NoDiscount_ShowsOnlyTheTotal()
        {
            // Assemble
            OrderPlaced orderPlaced = new()
            {
                Id = Guid.NewGuid(),
                CorrelationId = "c",
                CustomerId = 42,
                CustomerEmail = "customer@example.com",
                Subtotal = 199.00m,
                Total = 199.00m,
                PaymentMethod = PaymentMethods.BankTransfer,
                Status = OrderStatus.AwaitingPayment,
                CreatedDate = DateTimeOffset.UtcNow,
                Items = [new OrderPlacedLineItem { ProductId = 3, ProductName = "Monitor", UnitPrice = 199.00m, Quantity = 1 }]
            };

            // Act
            EmailMessage email = OrderPlacedOutboxMessageHandler.BuildConfirmationEmail(orderPlaced);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(email.Body, Does.Contain("awaiting your bank transfer"));
                Assert.That(email.Body, Does.Contain("- 1 x Monitor at $199.00 each: $199.00"));
                Assert.That(email.Body, Does.Not.Contain("Subtotal"));
                Assert.That(email.Body, Does.Not.Contain("Discount"));
                Assert.That(email.Body, Does.EndWith("Total: $199.00"));
            }
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_ClaimedByAnotherProcessor_SkipsMessage()
        {
            // Assemble: another instance holds a lease on the message.
            await PlaceBankTransferOrderAsync();
            await ClaimOnlyOutboxMessageAsync(DateTimeOffset.UtcNow.AddMinutes(5));

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(message.ProcessedDate, Is.Null);
                Assert.That(message.Attempts, Is.Zero);
            }

            await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
        }

        [Test]
        public async Task ProcessPendingMessagesAsync_LeaseExpired_ProcessesMessage()
        {
            // Assemble: the instance that claimed the message died; its lease has run out.
            await PlaceBankTransferOrderAsync();
            await ClaimOnlyOutboxMessageAsync(DateTimeOffset.UtcNow.AddSeconds(-1));

            // Act
            await _handler!.ProcessPendingMessagesAsync(CancellationToken.None);

            // Assert
            OrderPlacedOutboxMessage message = await GetOnlyOutboxMessageAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(message.ProcessedDate, Is.Not.Null);
                Assert.That(message.LockedUntil, Is.Null);
            }

            await _emailSender.ReceivedWithAnyArgs(1).SendAsync(default!, default);
        }

        [Test]
        public async Task Claim_TwoProcessorsAtOnce_OnlyFirstSaveSucceeds()
        {
            // Assemble: both processors loaded the message before either claimed it.
            await PlaceBankTransferOrderAsync();
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();
            using IOrderUnitOfWork first = unitOfWorkFactory.Create();
            using IOrderUnitOfWork second = unitOfWorkFactory.Create();
            long messageId = (await GetOnlyOutboxMessageAsync()).Id;
            OrderPlacedOutboxMessage firstCopy = (await first.OrderPlacedOutboxRepository.GetByIdAsync(messageId))!;
            OrderPlacedOutboxMessage secondCopy = (await second.OrderPlacedOutboxRepository.GetByIdAsync(messageId))!;

            // Act
            firstCopy.Claim(DateTimeOffset.UtcNow.AddMinutes(5));
            await first.SaveChangesAsync();
            secondCopy.Claim(DateTimeOffset.UtcNow.AddMinutes(5));

            // Assert
            Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
        }

        private async Task AddOutboxMessageAsync(int version, string message)
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                await unitOfWork.OrderPlacedOutboxRepository.AddAsync(new OrderPlacedOutboxMessage(new CreateOrderPlacedOutboxMessage
                {
                    Version = version,
                    Message = message,
                    CreatedDate = DateTimeOffset.UtcNow
                }));
                await unitOfWork.SaveChangesAsync();
            }
        }

        private async Task ClaimOnlyOutboxMessageAsync(DateTimeOffset lockedUntil)
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                OrderPlacedOutboxMessage message = (await unitOfWork.OrderPlacedOutboxRepository.GetAsync()).Single();
                message.Claim(lockedUntil);
                await unitOfWork.SaveChangesAsync();
            }
        }

        private async Task<PlaceOrderResponse> PlaceBankTransferOrderAsync()
        {
            IRequestHandler<PlaceOrderRequest, PlaceOrderResponse> placeOrderHandler =
                _serviceProvider!.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>();

            return await placeOrderHandler.HandleAsync(new PlaceOrderRequestBuilder().WithPaymentMethod(PaymentMethods.BankTransfer).Build(),
                TestHeaders.Create());
        }

        private async Task<OrderPlacedOutboxMessage> GetOnlyOutboxMessageAsync()
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                return (await unitOfWork.OrderPlacedOutboxRepository.FindAsync(m => m.Id > 0)).Single();
            }
        }
    }
}