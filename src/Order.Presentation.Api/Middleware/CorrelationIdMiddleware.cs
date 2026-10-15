using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Order.Application.Constants;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Infrastructure.Observability.Extensions;
using Order.Presentation.Api.Dependencies;
using System.Diagnostics;

namespace Order.Presentation.Api.Middleware
{
    /// <summary>
    /// Requires the CorrelationId header on API calls, adds it to the logging scope and the current trace,
    /// and echoes it on the response.
    /// </summary>
    public sealed class CorrelationIdMiddleware(IObservabilityManager observabilityManager, ILogger<CorrelationIdMiddleware> logger, RequestDelegate next)
    {
        private const string CorrelationIdTag = "correlation.id";
        private const string ProblemContentType = "application/problem+json";

        private static readonly string[] HeaderIgnorePaths =
        [
            EndpointRegistration.ApiReferencePath,
            EndpointRegistration.OpenApiPath,
            EndpointRegistration.HealthCheckPath,
            MetricsEndpointExtensions.MetricsEndpoint,
            "/favicon.ico"
        ];

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsIgnored(context.Request.Path))
            {
                await next(context);
                return;
            }

            StringValues correlationId = context.Request.Headers[RequestHeaderKeys.CorrelationId];
            if (StringValues.IsNullOrEmpty(correlationId))
            {
                await RejectMissingCorrelationIdAsync(context);
                return;
            }

            Activity.Current?.SetTag(CorrelationIdTag, correlationId.ToString());
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestHeaderKeys.CorrelationId] = correlationId;
                return Task.CompletedTask;
            });

            using (logger.BeginScope(new Dictionary<string, object> { [RequestHeaderKeys.CorrelationId] = correlationId.ToString() }))
            {
                await next(context);
            }
        }

        private static bool IsIgnored(PathString path)
        {
            return path == "/" || HeaderIgnorePaths.Any(ignoredPath => path.StartsWithSegments(ignoredPath, StringComparison.OrdinalIgnoreCase));
        }

        private async Task RejectMissingCorrelationIdAsync(HttpContext context)
        {
            string message = string.Format(ResponseMessages.HeaderRequired, RequestHeaderKeys.CorrelationId);
            observabilityManager.LogMessage(message).AsWarning();

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = message,
                Instance = context.Request.Path
            }, options: null, contentType: ProblemContentType);
        }
    }
}