using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.RateLimiting;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Presentation.Api.Authorization;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Order.Presentation.Api.Dependencies
{
    public static class RateLimitingRegistration
    {
        public const string OrdersPolicy = "orders";

        private const string ProblemContentType = "application/problem+json";

        /// <summary>
        /// Fixed window on the order endpoints, per customer (from the token), else per token subject, else per client IP.
        /// Behind a reverse proxy, enable forwarded headers so the client IP is the real one.
        /// </summary>
        public static void Register(IServiceCollection services, OrderConfiguration configuration)
        {
            RateLimitingConfiguration rateLimitingConfiguration = configuration.ApplicationConfiguration.RateLimiting;

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(OrdersPolicy, context => RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitingConfiguration.PermitLimit,
                    Window = rateLimitingConfiguration.Window,
                    QueueLimit = 0
                }));
                options.OnRejected = (context, cancellationToken) => OnRejectedAsync(context, rateLimitingConfiguration, cancellationToken);
            });
        }

        private static string GetPartitionKey(HttpContext context)
        {
            if (context.User.TryGetCustomerId(out int customerId))
            {
                return $"customer:{customerId}";
            }

            string? subject = context.User.FindFirst(OrderClaims.Subject)?.Value;

            return subject != null ? $"subject:{subject}" : $"ip:{context.Connection.RemoteIpAddress}";
        }

        private static async ValueTask OnRejectedAsync(OnRejectedContext context, RateLimitingConfiguration rateLimitingConfiguration, CancellationToken cancellationToken)
        {
            HttpContext httpContext = context.HttpContext;
            int retryAfterSeconds = (int)Math.Ceiling((context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
                ? retryAfter
                : rateLimitingConfiguration.Window).TotalSeconds);

            IObservabilityManager observabilityManager = httpContext.RequestServices.GetRequiredService<IObservabilityManager>();
            observabilityManager.RecordMetric(MetricDefinitions.ApiRateLimitRejectedCount).Record();

            string message = string.Format(ResponseMessages.TooManyRequests, retryAfterSeconds);
            observabilityManager.LogMessage(message).AsWarning();

            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = message,
                Instance = httpContext.Request.Path
            }, options: null, contentType: ProblemContentType, cancellationToken);
        }
    }
}