using Order.Application.Helpers;
using Order.Application.Models.Configuration;
using Order.Infrastructure.Persistence.Extensions;

namespace Order.Presentation.Api.Dependencies
{
    public static class HealthCheckRegistration
    {
        public static void Register(IHealthChecksBuilder healthChecksBuilder, OrderConfiguration configuration)
        {
            // In test mode the database is in memory, so there is no SQL Server to check.
            if (EnvironmentHelper.IsTestMode())
            {
                return;
            }

            healthChecksBuilder.AddPersistenceHealthChecks(configuration);
        }
    }
}