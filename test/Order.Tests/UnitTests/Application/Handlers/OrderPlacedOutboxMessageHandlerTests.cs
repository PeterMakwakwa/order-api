using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
                    && email.Body.Contains("$80.97")
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