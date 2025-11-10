// OptionsBindingExtensions.cs
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Namo.WIN.Infrastructure;
// OptionsBindingExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class OptionsBindingExtensions
{
    /// <summary>
    /// Binds TOptions from a configuration section (default: typeof(TOptions).Name).
    /// Only leaf keys whose names end with Path/Dir/Directory/File/Folder are expanded
    /// using Environment.ExpandEnvironmentVariables before binding.
    /// No post-mutation, so init-only setters are fully supported.
    /// </summary>
    public static OptionsBuilder<TOptions> ConfigureWithEnv<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null)
        where TOptions : class
    {
        sectionName ??= typeof(TOptions).Name;
        var section = configuration.GetSection(sectionName);

        // Flatten the section to key/value pairs (relative keys).
        var pairs = section.AsEnumerable(makePathsRelative: true)
                           .Where(kvp => kvp.Value is not null);

        // Build a projected dictionary with env-expanded values for path-like keys.
        var expanded = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            var leafName = GetLeafName(key);
            expanded[key] = IsPathLike(leafName)
                ? Environment.ExpandEnvironmentVariables(value!)
                : value;
        }

        // Re-bind from an in-memory configuration built from the expanded pairs.
        var proxyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(expanded)
            .Build();

        return services
            .AddOptions<TOptions>()
            .Bind(proxyConfig)        // bind from already-expanded values
            .ValidateDataAnnotations()
            .ValidateOnStart(); // optional; keep or remove as needed
    }

    private static string GetLeafName(string key)
    {
        var idx = key.LastIndexOf(':');
        return idx >= 0 ? key.Substring(idx + 1) : key;
    }

    private static bool IsPathLike(string name) =>
        name.EndsWith("Path", StringComparison.Ordinal) ||
        name.EndsWith("Dir", StringComparison.Ordinal) ||
        name.EndsWith("Directory", StringComparison.Ordinal) ||
        name.EndsWith("File", StringComparison.Ordinal) ||
        name.EndsWith("Folder", StringComparison.Ordinal);
}
