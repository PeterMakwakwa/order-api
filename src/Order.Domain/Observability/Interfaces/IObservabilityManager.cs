using Order.Domain.Observability.Models.Metrics.Definitions;
using System.Runtime.CompilerServices;

namespace Order.Domain.Observability.Interfaces
{
    /// <summary>
    /// Single entry point for logs, metrics and traces. Every log line and trace is prefixed with the
    /// calling class and member, so call sites only pass the message.
    /// </summary>
    public interface IObservabilityManager
    {
        public ILogManager LogMessage(string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "");

        public IMetric RecordMetric(MetricDefinition metric);

        public ITraceManager CreateTrace(string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "");
    }
}