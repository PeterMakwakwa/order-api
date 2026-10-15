using Order.Presentation.Api.Middleware;

namespace Order.Presentation.Api.Dependencies
{
    public static class MiddlewareRegistration
    {
        /// <summary>
        /// Outermost first: correlation id (required on API calls; everything below logs with it) →
        /// request logging and metrics (so they see the final status code) → exception mapping →
        /// problem details for empty error responses (401, 403, 404) → authentication → rate limiting
        /// (after authentication, so it can partition by customer) → authorization.
        /// </summary>
        public static void Register(WebApplication app)
        {
            app.UseRouting();

            app.UseMiddleware<CorrelationIdMiddleware>()
                .UseMiddleware<RequestObservabilityMiddleware>()
                .UseMiddleware<IntermediateExceptionHandler>();

            app.UseStatusCodePages();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();
        }
    }
}