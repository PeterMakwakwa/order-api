using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Order.Presentation.Api.HealthChecks
{
    /// <summary>
    /// JSON health report: overall status plus one entry per check (status, duration, failure description).
    /// </summary>
    internal static class HealthCheckResponseWriter
    {
        public static Task WriteAsync(HttpContext context, HealthReport report)
        {
            return context.Response.WriteAsJsonAsync(new
            {
                Status = report.Status.ToString(),
                TotalDuration = report.TotalDuration,
                Entries = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        Status = entry.Value.Status.ToString(),
                        entry.Value.Duration,
                        entry.Value.Description
                    })
            });
        }
    }
}