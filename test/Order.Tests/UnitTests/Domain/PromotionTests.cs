using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Entities.Exceptions;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Domain
{
    public sealed class PromotionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Constructor_LowerCaseCode_StoresUpperCase()
        {
            // Act
            Promotion promotion = new(DefaultPromotions.Welcome10 with { Code = " welcome10 " });

            // Assert
            Assert.That(promotion.Code, Is.EqualTo("WELCOME10"));
        }

        [Test]
        public void IsApplicableAt_ActiveWithoutEndDate_IsTrue()
        {
            // Assemble
            Promotion promotion = new(DefaultPromotions.Welcome10);

            // Act & Assert
            Assert.That(promotion.IsApplicableAt(Now), Is.True);
        }

        [Test]
        public void IsApplicableAt_Expired_IsFalse()
        {
            // Assemble
            Promotion promotion = new(DefaultPromotions.Expired);

            // Act & Assert
            Assert.That(promotion.IsApplicableAt(Now), Is.False);
        }

        [Test]
        public void IsApplicableAt_Inactive_IsFalse()
        {
            // Assemble
            Promotion promotion = new(DefaultPromotions.Inactive);

            // Act & Assert
            Assert.That(promotion.IsApplicableAt(Now), Is.False);
        }

        [Test]
        public void IsApplicableAt_BeforeStartOrAtEnd_IsFalse()
        {
            // Assemble
            Promotion promotion = new(DefaultPromotions.Welcome10 with { ValidFrom = Now, ValidTo = Now.AddDays(1) });

            // Act & Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(promotion.IsApplicableAt(Now.AddTicks(-1)), Is.False);
                Assert.That(promotion.IsApplicableAt(Now), Is.True);
                Assert.That(promotion.IsApplicableAt(Now.AddDays(1)), Is.False);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-0.1)]
        public void Constructor_DiscountRateOutOfRange_ThrowsDomainRuleException(decimal discountRate)
        {
            // Assemble
            CreatePromotion command = DefaultPromotions.Welcome10 with { DiscountRate = discountRate };

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Promotion(command));
        }

        [Test]
        public void Constructor_EndsBeforeItStarts_ThrowsDomainRuleException()
        {
            // Assemble
            CreatePromotion command = DefaultPromotions.Welcome10 with { ValidFrom = Now, ValidTo = Now };

            // Act & Assert
            Assert.Throws<DomainRuleException>(() => _ = new Promotion(command));
        }
    }
}