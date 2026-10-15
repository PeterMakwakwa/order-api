using System.Diagnostics;

namespace Order.Domain.Observability.Interfaces
{
    public interface ITraceManager
    {
        /// <summary>
        /// Starts a span. Returns null when nothing is listening (no tracer configured), so callers
        /// should use <c>activity?.</c>.
        /// </summary>
        Activity? Start();

        /// <summary>
        /// Starts a span inside the trace identified by <paramref name="correlationId"/> when it is a valid
        /// 32-character hex trace id; otherwise inside a new trace.
        /// </summary>
        Activity? Continue(string correlationId);
    }
}