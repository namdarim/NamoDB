using Microsoft.Extensions.Configuration;

namespace Namo.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

#if DEBUG
        builder.Configuration
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif

        builder.Configuration
            .AddEnvironmentVariables(prefix: "NAMO_");

        //builder.Services.AddMaui();

        return builder.Build();
    }
}