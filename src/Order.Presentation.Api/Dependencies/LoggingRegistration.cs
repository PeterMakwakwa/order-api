using Serilog;
using Serilog.Formatting.Compact;

namespace Order.Presentation.Api.Dependencies
{
    public static class LoggingRegistration
    {
        private const string LocalOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Serilog with levels from the "Serilog" configuration section. Logging scopes (e.g. the CorrelationId)
        /// become properties on every event. Console output is compact JSON, or readable text on Localhost.
        /// </summary>
        public static void Register(WebApplicationBuilder builder)
        {
            bool isLocalhost = builder.Environment.IsEnvironment(ConfigurationRegistration.LocalhostEnvironment);

            builder.Services.AddSerilog((services, logger) =>
            {
                logger
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

                if (isLocalhost)
                {
                    logger.WriteTo.Console(outputTemplate: LocalOutputTemplate);
                }
                else
                {
                    logger.WriteTo.Console(new CompactJsonFormatter());
                }
            });
        }
    }
}