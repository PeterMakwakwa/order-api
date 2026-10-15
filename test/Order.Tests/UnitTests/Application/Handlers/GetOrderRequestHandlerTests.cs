using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Tests.Builders;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Application.Handlers
{
    public sealed class GetOrderRequestHandlerTests
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
        public async Task HandleAsync_ExistingOrder_ReturnsOrderWithLineItems()
        {
            // Assemble
            PlaceOrderResponse placed = await _serviceProvider!.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>()
                .HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create());

            // Act
            GetOrderResponse response = await Handler().HandleAsync(new GetOrderRequest { OrderId = placed.OrderId });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.OrderId, Is.EqualTo(placed.OrderId));
                Assert.That(response.CustomerId, Is.EqualTo(PlaceOrderRequestBuilder.DefaultCustomerId));
                Assert.That(response.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(response.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
                Assert.That(response.DiscountRate, Is.EqualTo(DefaultPromotions.Welcome10.DiscountRate));
                Assert.That(response.Subtotal, Is.EqualTo(PlaceOrderRequestBuilder.DefaultSubtotal));
                Assert.That(response.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(response.Items.Select(item => (item.ProductId, item.ProductName, item.UnitPrice, item.Quantity)), Is.EqualTo(new[]
                {
                    (DefaultProducts.Keyboard.Id, DefaultProducts.Keyboard.Name, DefaultProducts.Keyboard.Price, 1),
                    (DefaultProducts.Mouse.Id, DefaultProducts.Mouse.Name, DefaultProducts.Mouse.Price, 2)
                }));
            }
        }

        [Test]
        public void HandleAsync_UnknownOrder_ThrowsOrderNotFound()
        {
            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() => Handler().HandleAsync(new GetOrderRequest { OrderId = Guid.NewGuid() }));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<OrderNotFoundException>());
        }

        private IRequestHandler<GetOrderRequest, GetOrderResponse> Handler()
        {
            return _serviceProvider!.GetRequiredService<IRequestHandler<GetOrderRequest, GetOrderResponse>>();
        }

        [Test]
        public async Task HandleAsync_AnotherCustomersOrder_ThrowsOrderNotFound()
        {
            // Assemble
            PlaceOrderResponse placed = await _serviceProvider!.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>()
                .HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create());

            // Act
            HandlerException? exception = Assert.ThrowsAsync<HandlerException>(() =>
                Handler().HandleAsync(new GetOrderRequest { OrderId = placed.OrderId, CustomerId = PlaceOrderRequestBuilder.DefaultCustomerId + 1 }));

            // Assert
            Assert.That(exception!.GetBaseException(), Is.TypeOf<OrderNotFoundException>());
        }

        [Test]
        public async Task HandleAsync_OwnOrder_ReturnsOrder()
        {
            // Assemble
            PlaceOrderResponse placed = await _serviceProvider!.GetRequiredService<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>>()
                .HandleAsync(new PlaceOrderRequestBuilder().Build(), TestHeaders.Create());

            // Act
            GetOrderResponse response = await Handler().HandleAsync(new GetOrderRequest { OrderId = placed.OrderId, CustomerId = PlaceOrderRequestBuilder.DefaultCustomerId });

            // Assert
            Assert.That(response.OrderId, Is.EqualTo(placed.OrderId));
        }
    }
}