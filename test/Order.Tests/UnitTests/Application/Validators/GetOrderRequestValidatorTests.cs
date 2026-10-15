using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Models.Request.V1;
using Order.Domain.Observability.Messages;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Application.Validators
{
    public sealed class GetOrderRequestValidatorTests
    {
        private ServiceProvider? _serviceProvider;
        private IValidator<GetOrderRequest>? _validator;

        [SetUp]
        public void Setup()
        {
            _serviceProvider = UnitTestSetup.Setup();
            _validator = _serviceProvider.GetRequiredService<IValidator<GetOrderRequest>>();
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task Validate_WhenOrderIdSet_ShouldNotHaveError()
        {
            TestValidationResult<GetOrderRequest> result = await _validator!.TestValidateAsync(new GetOrderRequest { OrderId = Guid.NewGuid() });

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Test]
        public async Task Validate_WhenOrderIdEmpty_ShouldHaveError()
        {
            TestValidationResult<GetOrderRequest> result = await _validator!.TestValidateAsync(new GetOrderRequest { OrderId = Guid.Empty });

            result.ShouldHaveValidationErrorFor(x => x.OrderId)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.OrderIdInvalid));
        }
    }
}