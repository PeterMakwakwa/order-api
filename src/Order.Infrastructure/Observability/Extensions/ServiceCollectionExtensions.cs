using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Observability;
using Order.Domain.Observability.Interfaces;

namespace Order.Infrastructure.Observability.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the observability manager. Metrics are scraped from /metrics; traces (the application's
        /// ActivitySource plus ASP.NET Core, HttpClient and SqlClient spans) are exported to an OTLP collector
        /// only when ApplicationConfiguration:Observability:OtlpEndpoint is set.
        /// </summary>
        public static IServiceCollection AddObservability(this IServiceCollection services, OrderConfiguration configuration)
        {
            services.AddSingleton<IObservabilityManager, ObservabilityManager>();

            ObservabilityConfiguration observabilityConfiguration = configuration.ApplicationConfiguration.Observability;
            if (string.IsNullOrWhiteSpace(observabilityConfiguration.OtlpEndpoint))
            {
                return services;
            }

            services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(observabilityConfiguration.ApplicationName))
                .WithTracing(tracing => tracing
                    .AddSource(observabilityConfiguration.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = new Uri(observabilityConfiguration.OtlpEndpoint)));

            return services;
        }
    }
}