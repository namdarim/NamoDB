using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Namo.App;
using Namo.Infrastructure.DBSync;
using Namo.WIN.Infrastructure;
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
                    // your custom extension
                    services
                            .AddNamoApp()
                            .AddNamoWinEnvironment(context.Configuration);
                })
                .Build();


            Application.Run(new FormMain(host.Services));

        }
    }
}