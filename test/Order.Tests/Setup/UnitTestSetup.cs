using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Helpers;
using Order.Application.Models.Configuration;
using Order.Presentation.Api;
using Order.Presentation.Api.Dependencies;

namespace Order.Tests.Setup
{
    public static class UnitTestSetup
    {
        public const string UnitTestEnvironment = "UnitTest";

        /// <summary>
        /// Matches ApplicationConfiguration:Observability:ApplicationName in appsettings.UnitTest.json (the Meter name).
        /// </summary>
        public const string ApplicationName = "Order.Api.Tests";

        /// <summary>
        /// The application's own registrations in test mode (in-memory database, no background polling).
        /// <paramref name="settings"/> override appsettings; <paramref name="configureServices"/> runs last, so it can replace services.
        /// </summary>
        public static ServiceProvider Setup(Action<IServiceCollection>? configureServices = null, IDictionary<string, string?>? settings = null)
        {
            SetupEnvironment();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.UnitTest.json")
                .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
                .Build();

            ServiceCollection services = [];
            services.AddLogging();

            OrderConfiguration orderConfiguration = ConfigurationRegistration.Register(services, configuration);
            DependencyRegistration.Register(services, orderConfiguration);

            configureServices?.Invoke(services);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// The real API (Program) on TestServer, in the UnitTest environment, accepting tokens from <see cref="TestTokens"/>.
        /// </summary>
        public static WebApplicationFactory<Program> SetupApplicationFactory(Action<IServiceCollection>? configureServices = null,
            IDictionary<string, string?>? settings = null)
        {
            SetupEnvironment();

            return new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment(UnitTestEnvironment);
                    builder.ConfigureAppConfiguration((_, configuration) => configuration
                        .AddJsonFile("appsettings.UnitTest.json")
                        .AddInMemoryCollection(TestTokens.AuthenticationSettings));

                    // UseSetting (not ConfigureAppConfiguration) so Program sees the overrides when it binds OrderConfiguration.
                    foreach (KeyValuePair<string, string?> setting in settings ?? new Dictionary<string, string?>())
                    {
                        builder.UseSetting(setting.Key, setting.Value);
                    }

                    builder.ConfigureTestServices(services => configureServices?.Invoke(services));
                });
        }

        private static void SetupEnvironment()
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.TestModeVariableName, bool.TrueString);
        }
    }
}