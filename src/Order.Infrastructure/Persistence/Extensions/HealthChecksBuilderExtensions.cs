using Microsoft.Extensions.DependencyInjection;
using Order.Application.Constants;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Persistence;

namespace Order.Infrastructure.Persistence.Extensions
{
    public static class HealthChecksBuilderExtensions
    {
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// One SQL Server check per configured database, part of readiness.
        /// </summary>
        public static IHealthChecksBuilder AddPersistenceHealthChecks(this IHealthChecksBuilder builder, OrderConfiguration configuration)
        {
            foreach (SqlServerConfiguration database in configuration.ApplicationConfiguration.Persistence.Values
                .Where(database => !string.IsNullOrWhiteSpace(database.Server) && !string.IsNullOrWhiteSpace(database.Database)))
            {
                builder.AddSqlServer(database.GetConnectionString(),
                    name: database.Database,
                    tags: [HealthCheckTags.Ready, "sqlserver"],
                    timeout: HealthCheckTimeout);
            }

            return builder;
        }
    }
}