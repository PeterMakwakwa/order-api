using Order.Application.Handlers.Extensions;
using Order.Application.HealthCheck.Extensions;
using Order.Application.Helpers;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Persistence;
using Order.Application.Validators.Extensions;
using Order.Domain.Observability.Messages;
using Order.Infrastructure.Email.Extensions;
using Order.Infrastructure.Observability.Extensions;
using Order.Infrastructure.Payments.Extensions;
using Order.Infrastructure.Persistence.Context;
using Order.Infrastructure.Persistence.Extensions;

namespace Order.Presentation.Api.Dependencies
{
    public static class DependencyRegistration
    {
        public const string ApplicationDatabase = "ApplicationDatabase";

        public static void Register(IServiceCollection services, OrderConfiguration configuration)
        {
            services
                .AddSingleton(TimeProvider.System)
                .AddObservability(configuration)
                .AddHandlers()
                .AddValidators()
                .AddDatabases(configuration)
                .AddUnitOfWorkFactory()
                .AddPaymentGateways(configuration)
                .AddEmailSender(configuration)
                .AddHealthCheckPublisher();
        }

        public static SqlServerConfiguration GetApplicationDatabase(OrderConfiguration configuration)
        {
            if (!configuration.ApplicationConfiguration.Persistence.TryGetValue(ApplicationDatabase, out SqlServerConfiguration? databaseConfiguration)
                || string.IsNullOrWhiteSpace(databaseConfiguration.Server))
            {
                throw new InvalidOperationException(string.Format(ErrorMessages.DatabaseConfigurationMissingErrorMessage, ApplicationDatabase));
            }

            if (!string.IsNullOrWhiteSpace(databaseConfiguration.Username) && string.IsNullOrWhiteSpace(databaseConfiguration.Password))
            {
                throw new InvalidOperationException(string.Format(ErrorMessages.DatabasePasswordMissingErrorMessage, ApplicationDatabase));
            }

            return databaseConfiguration;
        }

        private static IServiceCollection AddDatabases(this IServiceCollection services, OrderConfiguration configuration)
        {
            if (EnvironmentHelper.IsTestMode())
            {
                return services
                    .AddMockDatabase<OrderDatabaseContext>("OrderDatabase" + Guid.NewGuid());
            }

            return services
                .AddDatabase<OrderDatabaseContext>(GetApplicationDatabase(configuration).GetConnectionString());
        }
    }
}