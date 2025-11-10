using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Namo.App.Options;
using Namo.Common.Abstractions;
using Namo.Infrastructure.DBSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.WIN.Infrastructure;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNamoWinEnvironment(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IKeyValueStore, WinFileKeyValueStore>();
        services.ConfigureWithEnv<S3Settings>(configuration);
        services.ConfigureWithEnv<DbSyncPaths>(configuration);

        return services;
    }
}
