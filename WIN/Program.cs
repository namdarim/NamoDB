using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Namo.App.DBSync;
using Namo.Infrastructure.DBSync;
using Namo.WIN.Storage;
using SQLitePCL;

namespace Namo.WIN
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Batteries_V2.Init(); // must be called once before any SQLite use
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            using IHost host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<S3Settings>(
                         context.Configuration.GetSection(nameof(S3Settings)));
                    services.Configure<DbSyncPaths>(
                         context.Configuration.GetSection(nameof(DbSyncPaths))).PostConfigure<DbSyncPaths>(o =>
                         {
                             o.LocalDbPath = ResolvePath(o.LocalDbPath);
                             o.SnapshotDir = ResolvePath(o.SnapshotDir);
                             if (!string.IsNullOrWhiteSpace(o.ManifestPath))
                                 o.ManifestPath = ResolvePath(o.ManifestPath!);

                             // ensure directories exist
                             Directory.CreateDirectory(Path.GetDirectoryName(o.LocalDbPath)!);
                             Directory.CreateDirectory(o.SnapshotDir);
                             if (!string.IsNullOrWhiteSpace(o.ManifestPath))
                                 Directory.CreateDirectory(Path.GetDirectoryName(o.ManifestPath!)!);
                         });

                    static string ResolvePath(string s)
                    {
                        var expanded = Environment.ExpandEnvironmentVariables(s ?? string.Empty);
                        var normalized = expanded.Replace('/', Path.DirectorySeparatorChar);
                        return Path.GetFullPath(normalized);
                    }

                    // your custom extension
                    services.AddDbSync<WinFileKeyValueStore>();
                    services.AddSingleton<DbSyncAppService>();
                })
                .Build();


            Application.Run(new FormMain(host.Services));

        }
    }
}