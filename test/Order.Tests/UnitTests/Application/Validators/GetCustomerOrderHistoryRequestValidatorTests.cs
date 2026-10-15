using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Domain.Observability.Messages;
using Order.Tests.Setup;

namespace Order.Tests.UnitTests.Application.Validators
{
    public sealed class GetCustomerOrderHistoryRequestValidatorTests
    {
        private ServiceProvider? _serviceProvider;
        private IValidator<GetCustomerOrderHistoryRequest>? _validator;

        [SetUp]
        public void Setup()
        {
            _serviceProvider = UnitTestSetup.Setup();
            _validator = _serviceProvider.GetRequiredService<IValidator<GetCustomerOrderHistoryRequest>>();
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task Validate_WhenCustomerIdPositive_ShouldNotHaveError()
        {
            TestValidationResult<GetCustomerOrderHistoryRequest> result = await _validator!.TestValidateAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42 });

            result.ShouldNotHaveAnyValidationErrors();
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task Validate_WhenCustomerIdNotPositive_ShouldHaveError(int customerId)
        {
            TestValidationResult<GetCustomerOrderHistoryRequest> result = await _validator!.TestValidateAsync(new GetCustomerOrderHistoryRequest { CustomerId = customerId });

            result.ShouldHaveValidationErrorFor(x => x.CustomerId)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerIdInvalid));
        }

        [TestCase("")]
        [TestCase("not a cursor")]
        [TestCase("bm90LWEtY3Vyc29y")]
        [TestCase("MTIzOjA")]
        public async Task Validate_WhenCursorInvalid_ShouldHaveError(string cursor)
        {
            TestValidationResult<GetCustomerOrderHistoryRequest> result = await _validator!.TestValidateAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, Cursor = cursor });

            result.ShouldHaveValidationErrorFor(x => x.Cursor)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CursorInvalid));
        }

        [Test]
        public async Task Validate_WhenCursorValid_ShouldNotHaveError()
        {
            string cursor = new OrderHistoryCursor { CreatedDate = DateTimeOffset.UtcNow, Position = 7 }.Encode();

            TestValidationResult<GetCustomerOrderHistoryRequest> result = await _validator!.TestValidateAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, Cursor = cursor });

            result.ShouldNotHaveAnyValidationErrors();
        }

        [TestCase(0)]
        [TestCase(GetCustomerOrderHistoryRequest.MaxPageSize + 1)]
        public async Task Validate_WhenPageSizeOutOfRange_ShouldHaveError(int pageSize)
        {
            TestValidationResult<GetCustomerOrderHistoryRequest> result = await _validator!.TestValidateAsync(new GetCustomerOrderHistoryRequest { CustomerId = 42, PageSize = pageSize });

            result.ShouldHaveValidationErrorFor(x => x.PageSize)
                .WithErrorMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.PageSizeInvalid, GetCustomerOrderHistoryRequest.MaxPageSize)));
        }
    }
}