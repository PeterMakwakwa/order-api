using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Observability.Messages;
using Order.Tests.Builders;
using Order.Tests.Setup;
using System.Net;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Tests.UnitTests.Application.Validators
{
    public sealed class PlaceOrderRequestValidatorTests
    {
        private static readonly string BadRequestErrorCode = ((int)HttpStatusCode.BadRequest).ToString();

        private ServiceProvider? _serviceProvider;
        private IValidator<PlaceOrderRequest>? _validator;

        [SetUp]
        public void Setup()
        {
            _serviceProvider = UnitTestSetup.Setup();
            _validator = _serviceProvider.GetRequiredService<IValidator<PlaceOrderRequest>>();
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task Validate_WhenRequestValid_ShouldNotHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public async Task Validate_WhenCustomerIdNotPositive_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithCustomerId(0).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.CustomerId)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerIdInvalid))
                .WithErrorCode(BadRequestErrorCode);
        }

        [TestCase("")]
        [TestCase("not-an-email")]
        public async Task Validate_WhenCustomerEmailInvalid_ShouldHaveError(string customerEmail)
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithCustomerEmail(customerEmail).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.CustomerEmail)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerEmailInvalid));
        }

        [Test]
        public async Task Validate_WhenPaymentMethodUndefined_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPaymentMethod((PaymentMethods)99).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.PaymentMethod)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.PaymentMethodInvalid));
        }

        [Test]
        public async Task Validate_WhenNoPromotionCode_ShouldNotHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode(null).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public async Task Validate_WhenPromotionCodeEmpty_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode(string.Empty).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.PromotionCode)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.PromotionCodeEmpty))
                .WithErrorCode(BadRequestErrorCode);
        }

        [Test]
        public async Task Validate_WhenPromotionCodeTooLong_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithPromotionCode(new string('A', 51)).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.PromotionCode)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.PromotionCodeTooLong, 50)));
        }

        [Test]
        public async Task Validate_WhenItemsEmpty_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithItems([]).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.Items)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.ItemsInvalid));
        }

        [Test]
        public async Task Validate_WhenQuantityNotPositive_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = 0 }])
                .Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrors()
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.ItemQuantityInvalid));
        }

        [Test]
        public async Task Validate_WhenSeveralPropertiesInvalid_ShouldReportEveryOne()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithCustomerEmail("not-an-email")
                .WithPaymentMethod((PaymentMethods)99)
                .WithItems([])
                .Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            using (Assert.EnterMultipleScope())
            {
                result.ShouldHaveValidationErrorFor(x => x.CustomerEmail);
                result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
                result.ShouldHaveValidationErrorFor(x => x.Items);
            }
        }

        [Test]
        public async Task Validate_WhenCustomerEmailEmpty_ShouldReportItOnce()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithCustomerEmail(string.Empty).Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            Assert.That(result.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Validate_WhenTooManyItems_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([.. Enumerable.Range(1, OrderEntity.MaxLineItems + 1).Select(id => new OrderItem { ProductId = id, Quantity = 1 })])
                .Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.Items)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemsTooMany, OrderEntity.MaxLineItems)))
                .WithErrorCode(BadRequestErrorCode);
        }

        [Test]
        public async Task Validate_WhenQuantityAboveMaximum_ShouldHaveError()
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = OrderLineItem.MaxQuantity + 1 }])
                .Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.Items)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemQuantityTooLarge, OrderLineItem.MaxQuantity)));
        }

        [TestCase(OrderLineItem.MaxQuantity, 1)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public async Task Validate_WhenRepeatedProductAboveMaximumInTotal_ShouldHaveError(int firstQuantity, int secondQuantity)
        {
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems(
                [
                    new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = firstQuantity },
                    new OrderItem { ProductId = DefaultProducts.Keyboard.Id, Quantity = secondQuantity }
                ])
                .Build();

            TestValidationResult<PlaceOrderRequest> result = await _validator!.TestValidateAsync(request);

            result.ShouldHaveValidationErrorFor(x => x.Items)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemQuantityTooLarge, OrderLineItem.MaxQuantity)));
        }
    }
}