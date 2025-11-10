using Microsoft.Extensions.DependencyInjection;
using Namo.App.Internal.DbSync;
using Namo.App.Services;
using Namo.Common.Abstractions;
using Namo.Domain.DBSync;
using Namo.Infrastructure; // assuming this namespace has AddInfrastructure()

namespace Namo.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNamoApp(this IServiceCollection services)
    {
        // app-level services
        services.AddSingleton<IBackupNamer, DefaultBackupNamer>();
        services.AddSingleton<DbSyncAppService>();
        // pull in infrastructure registrations
        services.AddInfrastructure();
        return services;
    }
}
