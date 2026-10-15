using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Domain.UnitOfWork;
using Order.Infrastructure.Persistence.UnitOfWork;

namespace Order.Infrastructure.Persistence.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private const int MaxRetryCount = 5;

        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

        public static IServiceCollection AddUnitOfWorkFactory(this IServiceCollection services)
        {
            return services.AddTransient<IUnitOfWorkFactory<IOrderUnitOfWork>, OrderUnitOfWorkFactory>();
        }

        /// <summary>
        /// SQL Server with EF Core's retrying execution strategy for transient failures.
        /// </summary>
        public static IServiceCollection AddDatabase<T>(this IServiceCollection services, string connectionString)
            where T : DbContext
        {
            return services.AddDbContextFactory<T>(options =>
                options.UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, null)));
        }

        public static IServiceCollection AddMockDatabase<T>(this IServiceCollection services, string databaseName)
            where T : DbContext
        {
            return services.AddDbContextFactory<T>(options =>
                options.UseInMemoryDatabase(databaseName), ServiceLifetime.Transient);
        }
    }
}