using Order.Application.Models.Configuration;

namespace Order.Presentation.Api.Dependencies
{
    public static class ConfigurationRegistration
    {
        public const string LocalhostEnvironment = "Localhost";

        /// <summary>
        /// Binds appsettings + user-secrets + environment variables to <see cref="OrderConfiguration"/>
        /// and registers it as a singleton.
        /// </summary>
        public static OrderConfiguration Register(IServiceCollection services, IConfiguration configuration)
        {
            OrderConfiguration orderConfiguration = configuration.Get<OrderConfiguration>()
                ?? throw new InvalidOperationException($"The {nameof(OrderConfiguration.ApplicationConfiguration)} section is missing.");

            services.AddSingleton(orderConfiguration);

            return orderConfiguration;
        }

        /// <summary>
        /// The default host only loads user-secrets in "Development"; local runs use "Localhost".
        /// </summary>
        public static void AddLocalSecrets(WebApplicationBuilder builder)
        {
            if (builder.Environment.IsEnvironment(LocalhostEnvironment))
            {
                builder.Configuration.AddUserSecrets<Program>(optional: true);
            }
        }
    }
}