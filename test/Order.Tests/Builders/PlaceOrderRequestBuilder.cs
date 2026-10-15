using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Domain.Enums;
using Order.Tests.Setup;

namespace Order.Tests.Builders
{
    /// <summary>
    /// Default: customer 42, one keyboard + two mice, card payment, WELCOME10 (10% off) → (49.99 + 39.98) * 0.9 = 80.97.
    /// </summary>
    public class PlaceOrderRequestBuilder : BuilderBase<PlaceOrderRequest>
    {
        public const int DefaultCustomerId = 42;
        public const decimal DefaultSubtotal = 89.97m;
        public const decimal DefaultTotal = 80.97m;

        public PlaceOrderRequestBuilder() : base(GetDefaultRequest())
        {
        }

        public PlaceOrderRequestBuilder WithCustomerId(int customerId) => (PlaceOrderRequestBuilder)With(x => x.CustomerId, customerId);
        public PlaceOrderRequestBuilder WithCustomerEmail(string customerEmail) => (PlaceOrderRequestBuilder)With(x => x.CustomerEmail, customerEmail);
        public PlaceOrderRequestBuilder WithItems(List<OrderItem> items) => (PlaceOrderRequestBuilder)With(x => x.Items, items);
        public PlaceOrderRequestBuilder WithPaymentMethod(PaymentMethods paymentMethod) => (PlaceOrderRequestBuilder)With(x => x.PaymentMethod, paymentMethod);
        public PlaceOrderRequestBuilder WithPromotionCode(string? promotionCode) => (PlaceOrderRequestBuilder)With(x => x.PromotionCode, promotionCode);

        private static PlaceOrderRequest GetDefaultRequest()
        {
            return new PlaceOrderRequest
            {
                CustomerId = DefaultCustomerId,
                CustomerEmail = "customer@example.com",
                Items =
                [
                    new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = 1 },
                    new OrderItem { ProductId = DefaultProducts.Mouse.Id, Quantity = 2 }
                ],
                PaymentMethod = PaymentMethods.CreditCard,
                PromotionCode = DefaultPromotions.Welcome10.Code
            };
        }
    }
}