using Microsoft.AspNetCore.Mvc;
using Order.Application.Handlers.Exceptions;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;

namespace Order.Presentation.Api.Middleware
{
    /// <summary>
    /// Maps exceptions to RFC 7807 problem details. Handlers wrap failures in HandlerException, so the root
    /// cause (GetBaseException) decides the status code. Unknown errors become a generic 500: no exception
    /// details or stack traces ever reach the client. A DomainRuleException is one of them: the validators reject every
    /// input that could break a domain rule, so reaching one means a bug (for example, settling an order twice).
    /// </summary>
    public sealed class IntermediateExceptionHandler(IObservabilityManager observabilityManager, RequestDelegate next)
    {
        private const string ProblemContentType = "application/problem+json";

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            Exception baseException = exception.GetBaseException();
            int statusCode = baseException switch
            {
                OrderValidationException => StatusCodes.Status400BadRequest,
                PaymentDeclinedException => StatusCodes.Status402PaymentRequired,
                OrderNotFoundException => StatusCodes.Status404NotFound,
                DuplicatePaymentException => StatusCodes.Status409Conflict,
                IdempotentRequestInProgressException => StatusCodes.Status409Conflict,
                IdempotencyKeyReusedException => StatusCodes.Status422UnprocessableEntity,
                PaymentProviderUnavailableException => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            string title;
            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                title = ErrorMessages.UnhandledExceptionErrorMessage;
                observabilityManager.LogMessage(title).AsError(exception);
            }
            else
            {
                title = baseException.Message;
                observabilityManager.LogMessage(title).AsWarning();
            }

            ProblemDetails problemDetails = baseException is OrderValidationException orderValidationException
                ? new ValidationProblemDetails(orderValidationException.Errors.ToDictionary())
                : new ProblemDetails();
            problemDetails.Status = statusCode;
            problemDetails.Title = title;
            problemDetails.Instance = context.Request.Path;

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), options: null, contentType: ProblemContentType);
        }
    }
}