using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;

namespace Order.Presentation.Api.Middleware
{
    /// <summary>
    /// Logs every request with its status and duration, and records api.request.count and
    /// api.request.duration.ms by route template (not raw path, to keep metric cardinality bounded).
    /// </summary>
    public sealed class RequestObservabilityMiddleware(IObservabilityManager observabilityManager, TimeProvider timeProvider, RequestDelegate next)
    {
        private const string UnmatchedEndpoint = "unmatched";

        public async Task InvokeAsync(HttpContext context)
        {
            long startTimestamp = timeProvider.GetTimestamp();

            try
            {
                await next(context);
                Record(context, context.Response.StatusCode, startTimestamp);
            }
            catch
            {
                // Only reached if an exception escapes IntermediateExceptionHandler; the server then answers 500.
                Record(context, StatusCodes.Status500InternalServerError, startTimestamp);
                throw;
            }
        }

        private void Record(HttpContext context, int statusCode, long startTimestamp)
        {
            long elapsedMilliseconds = (long)timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string endpoint = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnmatchedEndpoint;
            KeyValuePair<string, object?>[] tags =
            [
                new(MetricDimensions.Endpoint, endpoint),
                new(MetricDimensions.HttpMethod, context.Request.Method),
                new(MetricDimensions.StatusCode, statusCode)
            ];

            observabilityManager.RecordMetric(MetricDefinitions.ApiRequestCount).Record(1, tags);
            observabilityManager.RecordMetric(MetricDefinitions.ApiRequestDuration).Record(elapsedMilliseconds, tags);

            observabilityManager.LogMessage(string.Format(InfoMessages.RequestCompleted,
                context.Request.Method, context.Request.Path, statusCode, elapsedMilliseconds)).AsInfo();
        }
    }
}