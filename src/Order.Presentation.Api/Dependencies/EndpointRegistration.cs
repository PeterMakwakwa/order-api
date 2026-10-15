using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Order.Application.Constants;
using Order.Infrastructure.Observability.Extensions;
using Order.Presentation.Api.HealthChecks;
using Scalar.AspNetCore;

namespace Order.Presentation.Api.Dependencies
{
    public static class EndpointRegistration
    {
        public const string HealthCheckPath = "/healthz";
        public const string LivenessPath = HealthCheckPath + "/live";
        public const string ReadinessPath = HealthCheckPath + "/ready";
        public const string ApiReferencePath = "/scalar";
        public const string OpenApiPath = "/openapi";

        public static void Register(WebApplication app)
        {
            app.MapControllers();
            app.MapOpenApi().WithDocumentPerVersion();
            app.MapScalarApiReference(ApiReferencePath, options => options.WithTitle("Orders API"));

            // Liveness: the process is up (no dependency checks, so a database outage never restarts the pod).
            // Readiness: the dependencies tagged "ready" (SQL Server) are reachable, so the instance can take traffic.
            app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthCheckResponseWriter.WriteAsync });
            app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            });

            app.MapGet("/", () => Results.Redirect($"{ApiReferencePath}/v1")).ExcludeFromDescription();

            app.AddMetricsEndpoint();
        }
    }
}