using Order.Application.Models.Configuration;
using Order.Presentation.Api.Dependencies;

namespace Order.Presentation.Api
{
    public class Program
    {
        protected Program()
        {
        }

        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            ConfigurationRegistration.AddLocalSecrets(builder);
            OrderConfiguration configuration = ConfigurationRegistration.Register(builder.Services, builder.Configuration);

            LoggingRegistration.Register(builder);
            DependencyRegistration.Register(builder.Services, configuration);
            AuthenticationRegistration.Register(builder.Services);
            RateLimitingRegistration.Register(builder.Services, configuration);
            ControllerRegistration.Register(builder.Services);
            HealthCheckRegistration.Register(builder.Services.AddHealthChecks(), configuration);

            WebApplication app = builder.Build();

            MiddlewareRegistration.Register(app);
            EndpointRegistration.Register(app);

            await app.RunAsync();
        }
    }
}