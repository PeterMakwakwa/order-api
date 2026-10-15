using FluentValidation;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Domain.Observability.Messages;
using System.Net;

namespace Order.Application.Validators.Request.V1
{
    internal sealed class GetCustomerOrderHistoryRequestValidator : AbstractValidator<GetCustomerOrderHistoryRequest>
    {
        private static readonly string BadRequestErrorCode = ((int)HttpStatusCode.BadRequest).ToString();

        public GetCustomerOrderHistoryRequestValidator()
        {
            RuleFor(x => x.CustomerId)
                .GreaterThan(0)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerIdInvalid))
                .WithErrorCode(BadRequestErrorCode);

            RuleFor(x => x.Cursor)
                .Must(cursor => OrderHistoryCursor.TryDecode(cursor, out _))
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CursorInvalid))
                .WithErrorCode(BadRequestErrorCode)
                .When(x => x.Cursor != null);

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, GetCustomerOrderHistoryRequest.MaxPageSize)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.PageSizeInvalid, GetCustomerOrderHistoryRequest.MaxPageSize)))
                .WithErrorCode(BadRequestErrorCode);
        }
    }
}