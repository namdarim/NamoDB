using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Namo.Infrastructure.DBSync;
using Namo.WIN.Storage;

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

                    // your custom extension
                    services.AddDbSync<WinFileKeyValueStore>();

                })
                .Build();

           
            Application.Run(new FormMain(host.Services));

        }
    }
}