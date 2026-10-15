using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Order.Application.Handlers.Abstractions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Tests.Builders;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Application.Handlers
{
    public sealed class GetCustomerOrderHistoryRequestHandlerTests
    {
        private ServiceProvider? _serviceProvider;

        [SetUp]
        public async Task SetupAsync()
        {
            IPaymentGateway paymentGateway = Substitute.For<IPaymentGateway>();
            paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Succeeded());
            IPaymentGatewayResolver paymentGatewayResolver = Substitute.For<IPaymentGatewayResolver>();
            paymentGatewayResolver.Resolve(Arg.Any<PaymentMethods>()).Returns(paymentGateway);

            _serviceProvider = UnitTestSetup.Setup(services => services.AddTransient(_ => paymentGatewayResolver));
            await DefaultDataSetup.AddDefaultDataAsync(_serviceProvider);
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task HandleAsync_CustomerWithOrders_ReturnsOnlyTheirOrders()
        {
            // Assemble
            PlaceOrderResponse placed = await PlaceOrderAsync(customerId: 42);
            await PlaceOrderAsync(customerId: 7);

            // Act
            GetCustomerOrderHistoryResponse response = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42 });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.CustomerId, Is.EqualTo(42));
                Assert.That(response.Orders, Has.Count.EqualTo(1));
                Assert.That(response.Orders[0].Id, Is.EqualTo(placed.OrderId));
                Assert.That(response.Orders[0].Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(response.Orders[0].Status, Is.EqualTo(OrderStatus.Completed));
            }
        }

        [Test]
        public async Task HandleAsync_CustomerWithoutOrders_ReturnsEmptyList()
        {
            // Act
            GetCustomerOrderHistoryResponse response = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 1000 });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Orders, Is.Empty);
                Assert.That(response.NextCursor, Is.Null);
            }
        }

        [Test]
        public async Task HandleAsync_FollowingNextCursor_WalksAllOrdersNewestFirst()
        {
            // Assemble
            PlaceOrderResponse oldest = await PlaceOrderAsync(customerId: 42);
            PlaceOrderResponse middle = await PlaceOrderAsync(customerId: 42);
            PlaceOrderResponse newest = await PlaceOrderAsync(customerId: 42);

            // Act
            GetCustomerOrderHistoryResponse firstPage = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = 2 });
            GetCustomerOrderHistoryResponse secondPage = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = 2, Cursor = firstPage.NextCursor });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstPage.Orders.Select(o => o.Id), Is.EqualTo(new[] { newest.OrderId, middle.OrderId }));
                Assert.That(firstPage.NextCursor, Is.Not.Null);
                Assert.That(secondPage.Orders.Select(o => o.Id), Is.EqualTo(new[] { oldest.OrderId }));
                Assert.That(secondPage.NextCursor, Is.Null);
                Assert.That(firstPage.TotalCount, Is.EqualTo(3));
                Assert.That(secondPage.PageSize, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task HandleAsync_OrderPlacedWhilePaging_DoesNotShiftTheNextPage()
        {
            // Assemble: with offset paging the new order would push the first page's order onto the second page again.
            PlaceOrderResponse oldest = await PlaceOrderAsync(customerId: 42);
            await PlaceOrderAsync(customerId: 42);
            GetCustomerOrderHistoryResponse firstPage = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = 1 });
            await PlaceOrderAsync(customerId: 42);

            // Act
            GetCustomerOrderHistoryResponse secondPage = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = 1, Cursor = firstPage.NextCursor });

            // Assert
            Assert.That(secondPage.Orders.Single().Id, Is.EqualTo(oldest.OrderId));
        }

        [Test]
        public async Task HandleAsync_ExactlyOnePageOfOrders_HasNoNextCursor()
        {
            // Assemble
            await PlaceOrderAsync(customerId: 42);

            // Act
            GetCustomerOrderHistoryResponse response = await Handler().HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = 1 });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Orders, Has.Count.EqualTo(1));
                Assert.That(response.NextCursor, Is.Null);
            }
        }

        private IRequestHandler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse> Handler()
        {
            return _serviceProvider!.GetRequiredService<IRequestHandler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse>>();
        }

        private async Task<PlaceOrderResponse> PlaceOrderAsync(int customerId)
        {
            IRequestHandler<PlaceOrderRequest, PlaceOrderResponse> handler =
                _serviceProvider!.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>();

            return await handler.HandleAsync(new PlaceOrderRequestBuilder().WithCustomerId(customerId).Build(), TestHeaders.Create());
        }
    }
}