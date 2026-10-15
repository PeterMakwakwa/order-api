using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.Application.HealthCheck.Publisher;

namespace Order.Application.HealthCheck.Extensions
{
    public static class HealthChecksBuilderExtensions
    {
        /// <summary>
        /// Publishes every health check result as a metric. The checks themselves are registered by the
        /// infrastructure that owns the resource (e.g. AddPersistenceHealthChecks).
        /// </summary>
        public static IServiceCollection AddHealthCheckPublisher(this IServiceCollection services)
        {
            return services
                .AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
        }
    }
}