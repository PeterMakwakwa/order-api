using Order.Domain.Observability.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Order.Infrastructure.Observability.Managers
{
    public sealed partial class TraceManager(ActivitySource activitySource, string className, string memberName, string message) : ITraceManager
    {
        public Activity? Start()
        {
            return activitySource.StartActivity(BuildName(), ActivityKind.Internal);
        }

        public Activity? Continue(string correlationId)
        {
            ActivityTraceId traceId = TraceIdRegex().IsMatch(correlationId)
                ? ActivityTraceId.CreateFromString(correlationId.AsSpan())
                : ActivityTraceId.CreateRandom();

            ActivityContext parentContext = new(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            return activitySource.StartActivity(BuildName(), ActivityKind.Internal, parentContext);
        }

        private string BuildName()
        {
            return $"{className}.{memberName}: {message}";
        }

        [GeneratedRegex("^[0-9a-fA-F]{32}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
        private static partial Regex TraceIdRegex();
    }
}