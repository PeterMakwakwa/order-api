using FluentValidation;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Domain.Entities;
using Order.Domain.Observability.Messages;
using System.Net;

namespace Order.Application.Validators.Request.V1
{
    /// <summary>
    /// Shape of the request only. Whether the products and the promotion code exist is checked by the handler,
    /// after the idempotency check, so a replayed request returns its original result.
    /// Every property is validated, so all problems are reported at once; within one property the first failed rule wins.
    /// The limits match the Order aggregate's, so a request that passes can never break a domain rule.
    /// </summary>
    internal sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
    {
        public const int PromotionCodeMaxLength = 50;

        private static readonly string BadRequestErrorCode = ((int)HttpStatusCode.BadRequest).ToString();

        public PlaceOrderRequestValidator()
        {
            AddValidation();
        }

        private void AddValidation()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            AddRuleForCustomerId();
            AddRuleForCustomerEmail();
            AddRuleForPaymentMethod();
            AddRuleForPromotionCode();
            AddRuleForItems();
        }

        private void AddRuleForCustomerId()
        {
            // Comes from the access token; a token without a valid customer id never gets this far.
            RuleFor(x => x.CustomerId)
                .GreaterThan(0)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerIdInvalid))
                .WithErrorCode(BadRequestErrorCode);
        }

        private void AddRuleForCustomerEmail()
        {
            RuleFor(x => x.CustomerEmail)
                .NotEmpty()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerEmailInvalid))
                .WithErrorCode(BadRequestErrorCode)
                .EmailAddress()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerEmailInvalid))
                .WithErrorCode(BadRequestErrorCode);
        }

        private void AddRuleForPaymentMethod()
        {
            RuleFor(x => x.PaymentMethod)
                .IsInEnum()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.PaymentMethodInvalid))
                .WithErrorCode(BadRequestErrorCode);
        }

        private void AddRuleForPromotionCode()
        {
            RuleFor(x => x.PromotionCode)
                .NotEmpty()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.PromotionCodeEmpty))
                .WithErrorCode(BadRequestErrorCode)
                .MaximumLength(PromotionCodeMaxLength)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.PromotionCodeTooLong, PromotionCodeMaxLength)))
                .WithErrorCode(BadRequestErrorCode)
                .When(x => x.PromotionCode != null);
        }

        private void AddRuleForItems()
        {
            RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.ItemsInvalid))
                .WithErrorCode(BadRequestErrorCode)
                .Must(items => items.Count <= Domain.Entities.Order.MaxLineItems)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemsTooMany, Domain.Entities.Order.MaxLineItems)))
                .WithErrorCode(BadRequestErrorCode)
                .Must(HaveQuantityWithinLimitPerProduct)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemQuantityTooLarge, OrderLineItem.MaxQuantity)))
                .WithErrorCode(BadRequestErrorCode);

            RuleForEach(x => x.Items).ChildRules(AddRuleForItemLine);
        }

        /// <summary>
        /// Repeated products are merged into one line, so the limit applies to each product's total quantity. Summed as
        /// long, so large quantities cannot overflow past the check.
        /// </summary>
        private static bool HaveQuantityWithinLimitPerProduct(List<OrderItem> items)
        {
            return items
                .GroupBy(i => i.ProductId)
                .All(group => group.Sum(i => (long)i.Quantity) <= OrderLineItem.MaxQuantity);
        }

        private static void AddRuleForItemLine(InlineValidator<OrderItem> item)
        {
            item.RuleFor(i => i.ProductId)
                .GreaterThan(0)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.ItemProductIdInvalid))
                .WithErrorCode(BadRequestErrorCode);

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.ItemQuantityInvalid))
                .WithErrorCode(BadRequestErrorCode);
        }
    }
}