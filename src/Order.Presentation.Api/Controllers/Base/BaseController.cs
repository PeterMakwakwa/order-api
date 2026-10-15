using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;

namespace Order.Presentation.Api.Controllers.Base
{
    /// <summary>
    /// Validate → handle → respond. A failed validation returns the status code carried in the rule's
    /// ErrorCode (e.g. "400") with every error as problem details; handler exceptions are mapped by the
    /// IntermediateExceptionHandler middleware.
    /// </summary>
    [ApiController]
    public abstract class BaseController(IObservabilityManager observabilityManager) : ControllerBase
    {
        protected IObservabilityManager ObservabilityManager => observabilityManager;

        /// <summary>
        /// Validates the request, then handles it. buildResult turns the response into the success result (200 OK when omitted).
        /// </summary>
        protected async Task<IActionResult> ProcessRequest<TRequest, TResponse>(
            Func<TRequest, CancellationToken, Task<ValidationResult>> validateAsync,
            Func<TRequest, Dictionary<string, string>?, CancellationToken, Task<TResponse>> handleAsync,
            TRequest request,
            Dictionary<string, string>? headers = null,
            Func<TResponse, IActionResult>? buildResult = null,
            CancellationToken cancellationToken = default)
        {
            ObservabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            ValidationResult validationResult = await validateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return BuildValidationProblem(validationResult);
            }

            TResponse response = await handleAsync(request, headers, cancellationToken);

            return buildResult != null ? buildResult(response) : Ok(response);
        }

        protected ObjectResult NotFoundProblem(string title)
        {
            return Problem(title: title, statusCode: StatusCodes.Status404NotFound, instance: HttpContext.Request.Path);
        }

        private ObjectResult BuildValidationProblem(ValidationResult validationResult)
        {
            int statusCode = int.TryParse(validationResult.Errors[0].ErrorCode, out int errorCode) && errorCode is >= 400 and < 500
                ? errorCode
                : StatusCodes.Status400BadRequest;

            Dictionary<string, string[]> errors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

            return new ObjectResult(new ValidationProblemDetails(errors)
            {
                Status = statusCode,
                Instance = HttpContext.Request.Path
            })
            {
                StatusCode = statusCode
            };
        }
    }
}