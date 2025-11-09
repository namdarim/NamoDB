using Microsoft.Extensions.DependencyInjection;
using Namo.Common.Abstractions;
using Namo.WIN.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.WIN;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNamoWinEnvironment(this IServiceCollection services)
    {
        services.AddSingleton<IKeyValueStore, WinFileKeyValueStore>();
        return services;
    }
}
