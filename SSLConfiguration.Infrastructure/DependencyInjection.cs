using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSLConfiguration.Infrastructure.Persistence;

namespace SSLConfiguration.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            string? connectionString = configuration.GetConnectionString("SSLConfigurationEntities");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing ConnectionStrings:SSLConfigurationEntities. " +
                    "Set it in appsettings.Development.json (local) or environment/user-secrets (non-dev).");
            }

            DbConfig.Initialize(connectionString);

            services.AddDbContext<SSLConfigurationEntities>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.CommandTimeout(60);
                    sqlOptions.EnableRetryOnFailure(3);
                }));

            return services;
        }
    }
}
