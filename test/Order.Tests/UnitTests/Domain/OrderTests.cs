using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Enums;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Domain = Order.Domain.Entities;

namespace Order.Tests.UnitTests.Domain
{
    public sealed class OrderTests
    {
        private static readonly DateTimeOffset RetriedDate = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Constructor_ValidCommand_CalculatesTotalsWithDiscount()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().Build();

            // Act
            Domain::Order order = new(command);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.UniqueId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(order.Subtotal, Is.EqualTo(89.97m));
                Assert.That(order.Total, Is.EqualTo(80.97m));
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PendingPayment));
                Assert.That(order.IsSettled, Is.False);
                Assert.That(order.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
                Assert.That(order.LineItems, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public void Constructor_RepeatedProduct_MergesIntoOneLineItem()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder()
                .WithPromotionCode(null)
                .WithDiscountRate(0)
                .WithLineItems([CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, 1), CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, 2)])
                .Build();

            // Act
            Domain::Order order = new(command);

            // Assert
            OrderLineItem lineItem = order.LineItems.Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lineItem.Quantity, Is.EqualTo(3));
                Assert.That(order.Total, Is.EqualTo(59.97m));
            }
        }

        [Test]
        public void Constructor_NoLineItems_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().WithLineItems([]).Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [TestCase(-0.1)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void Constructor_DiscountOutOfRange_ThrowsDomainRuleException(decimal discountRate)
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().WithDiscountRate(discountRate).Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void Constructor_ZeroQuantity_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder()
                .WithLineItems([CreateOrderCommandBuilder.LineItem(DefaultProducts.Keyboard, 0)])
                .Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [TestCase(PaymentOutcome.Succeeded, OrderStatus.Completed)]
        [TestCase(PaymentOutcome.Pending, OrderStatus.AwaitingPayment)]
        public void ProcessPaymentOutcome_Accepted_SetsStatusWithoutFailureReason(PaymentOutcome outcome, OrderStatus expectedStatus)
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());

            // Act
            order.Process(new RecordPaymentOutcome { Outcome = outcome, FailureReason = "ignored" });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(expectedStatus));
                Assert.That(order.PaymentOutcome, Is.EqualTo(outcome));
                Assert.That(order.PaymentFailureReason, Is.Null);
                Assert.That(order.IsSettled, Is.True);
            }
        }

        [TestCase(PaymentOutcome.Rejected)]
        [TestCase(PaymentOutcome.Duplicate)]
        [TestCase(PaymentOutcome.ProviderError)]
        public void ProcessPaymentOutcome_NotAccepted_FailsWithReason(PaymentOutcome outcome)
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());

            // Act
            order.Process(new RecordPaymentOutcome { Outcome = outcome, FailureReason = "Card declined" });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PaymentFailed));
                Assert.That(order.PaymentOutcome, Is.EqualTo(outcome));
                Assert.That(order.PaymentFailureReason, Is.EqualTo("Card declined"));
            }
        }

        [Test]
        public void ProcessPaymentOutcome_AlreadySettled_ThrowsDomainRuleException()
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());
            order.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.Succeeded });

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => order.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.Rejected }));
        }

        [Test]
        public void ProcessPaymentOutcome_UndefinedOutcome_ThrowsDomainRuleException()
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => order.Process(new RecordPaymentOutcome { Outcome = (PaymentOutcome)99 }));
        }

        [Test]
        public void Constructor_DiscountWithoutPromotionCode_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().WithPromotionCode(null).Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void Constructor_NoPromotion_ChargesFullPrice()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().WithPromotionCode(null).WithDiscountRate(0).Build();

            // Act
            Domain::Order order = new(command);

            // Assert
            Assert.That(order.Total, Is.EqualTo(order.Subtotal));
        }

        [Test]
        public void Constructor_MissingIdempotencyKey_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder().WithIdempotencyKey(" ").Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void RetryPayment_AfterProviderError_MovesBackToPendingPayment()
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());
            order.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.ProviderError, FailureReason = "Provider down" });
            Guid tokenBeforeRetry = order.ConcurrencyToken;

            // Act
            order.RetryPayment(RetriedDate);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PendingPayment));
                Assert.That(order.PaymentOutcome, Is.Null);
                Assert.That(order.PaymentFailureReason, Is.Null);
                Assert.That(order.ConcurrencyToken, Is.Not.EqualTo(tokenBeforeRetry));
                Assert.That(order.PaymentStartedDate, Is.EqualTo(RetriedDate));
            }
        }

        [TestCase(PaymentOutcome.Succeeded)]
        [TestCase(PaymentOutcome.Pending)]
        [TestCase(PaymentOutcome.Rejected)]
        [TestCase(PaymentOutcome.Duplicate)]
        public void RetryPayment_AfterFinalOutcome_ThrowsDomainRuleException(PaymentOutcome outcome)
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());
            order.Process(new RecordPaymentOutcome { Outcome = outcome });

            // Act & Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.CanRetryPayment, Is.False);
                Assert.Throws<DomainRuleException>(() => order.RetryPayment(RetriedDate));
            }
        }

        [Test]
        public void RetryPayment_WhilePending_ThrowsDomainRuleException()
        {
            // Assemble
            Domain::Order order = new(new CreateOrderCommandBuilder().Build());

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => order.RetryPayment(RetriedDate));
        }

        [Test]
        public void Constructor_QuantityAboveMaximum_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder()
                .WithLineItems([CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, OrderLineItem.MaxQuantity + 1)])
                .Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void Constructor_RepeatedProductAboveMaximumWhenMerged_ThrowsDomainRuleExceptionInsteadOfOverflowing()
        {
            // Assemble: int.MaxValue twice would wrap round to a negative quantity without the check.
            CreateOrder command = new CreateOrderCommandBuilder()
                .WithLineItems([CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, OrderLineItem.MaxQuantity), CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, 1)])
                .Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void Constructor_TooManyLineItems_ThrowsDomainRuleException()
        {
            // Assemble
            CreateOrder command = new CreateOrderCommandBuilder()
                .WithLineItems([.. Enumerable.Repeat(CreateOrderCommandBuilder.LineItem(DefaultProducts.Mouse, 1), Domain::Order.MaxLineItems + 1)])
                .Build();

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Domain::Order(command));
        }

        [Test]
        public void IsPaymentStalledAt_PendingLongerThanTheTimeout_IsTrueOnlyAfterTheTimeout()
        {
            // Assemble
            DateTimeOffset createdDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Domain::Order order = new(new CreateOrderCommandBuilder().WithCreatedDate(createdDate).Build());

            // Act & Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.IsPaymentStalledAt(createdDate.AddMinutes(4), TimeSpan.FromMinutes(5)), Is.False);
                Assert.That(order.IsPaymentStalledAt(createdDate.AddMinutes(6), TimeSpan.FromMinutes(5)), Is.True);
            }
        }

        [Test]
        public void IsPaymentStalledAt_SettledOrder_IsFalse()
        {
            // Assemble
            DateTimeOffset createdDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Domain::Order order = new(new CreateOrderCommandBuilder().WithCreatedDate(createdDate).Build());
            order.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.Succeeded });

            // Act & Assert
            Assert.That(order.IsPaymentStalledAt(createdDate.AddDays(1), TimeSpan.FromMinutes(5)), Is.False);
        }
    }
}