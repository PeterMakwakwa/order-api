using FluentValidation;
using Order.Application.Models.Request.V1;
using Order.Domain.Observability.Messages;
using System.Net;

namespace Order.Application.Validators.Request.V1
{
    internal sealed class GetOrderRequestValidator : AbstractValidator<GetOrderRequest>
    {
        public GetOrderRequestValidator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty()
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.OrderIdInvalid))
                .WithErrorCode(((int)HttpStatusCode.BadRequest).ToString());

            RuleFor(x => x.CustomerId)
                .GreaterThan(0)
                .WithMessage(string.Format(ResponseMessages.RequestValidationFailure, ResponseMessages.CustomerIdInvalid))
                .WithErrorCode(((int)HttpStatusCode.BadRequest).ToString())
                .When(x => x.CustomerId != null);
        }
    }
}