using Order.Domain.Commands;
using Order.Domain.Enums;
using Order.Tests.Setup;

namespace Order.Tests.Builders
{
    public class CreateOrderCommandBuilder : BuilderBase<CreateOrder>
    {
        public CreateOrderCommandBuilder() : base(GetDefaultCommand())
        {
        }

        public CreateOrderCommandBuilder WithCustomerId(int customerId) => (CreateOrderCommandBuilder)With(x => x.CustomerId, customerId);
        public CreateOrderCommandBuilder WithIdempotencyKey(string idempotencyKey) => (CreateOrderCommandBuilder)With(x => x.IdempotencyKey, idempotencyKey);
        public CreateOrderCommandBuilder WithPaymentMethod(PaymentMethods paymentMethod) => (CreateOrderCommandBuilder)With(x => x.PaymentMethod, paymentMethod);
        public CreateOrderCommandBuilder WithPromotionCode(string? promotionCode) => (CreateOrderCommandBuilder)With(x => x.PromotionCode, promotionCode);
        public CreateOrderCommandBuilder WithDiscountRate(decimal discountRate) => (CreateOrderCommandBuilder)With(x => x.DiscountRate, discountRate);
        public CreateOrderCommandBuilder WithCreatedDate(DateTimeOffset createdDate) => (CreateOrderCommandBuilder)With(x => x.CreatedDate, createdDate);
        public CreateOrderCommandBuilder WithLineItems(List<CreateOrderLineItem> lineItems) => (CreateOrderCommandBuilder)With(x => x.LineItems, lineItems);

        public static CreateOrderLineItem LineItem(CreateProduct product, int quantity)
        {
            return new CreateOrderLineItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity
            };
        }

        private static CreateOrder GetDefaultCommand()
        {
            return new CreateOrder
            {
                CorrelationId = Guid.NewGuid().ToString(),
                IdempotencyKey = Guid.NewGuid().ToString(),
                RequestHash = new string('a', 64),
                CustomerId = 42,
                CustomerEmail = "customer@example.com",
                PaymentMethod = PaymentMethods.CreditCard,
                PromotionCode = DefaultPromotions.Welcome10.Code,
                DiscountRate = 0.1m,
                CreatedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                LineItems = [LineItem(DefaultProducts.Keyboard, 1), LineItem(DefaultProducts.Mouse, 2)]
            };
        }
    }
}