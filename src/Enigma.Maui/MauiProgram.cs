using Enigma.SharedUI;
using Enigma.SharedUI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace Enigma.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // appsettings.json embebido: Api:BaseUrl = http://10.0.2.2:8081 (loopback del
        // host visto desde el emulador Android). Dispositivo físico: IP LAN.
        ConfigurationManager config = new();
        using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
        config.AddJsonStream(stream);

        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddSingleton<IConfiguration>(config);

        // HttpClient con cookie de sesión hacia la Api; luego los servicios de la RCL.
        builder.Services.AddScoped<CookieHandler>();
        builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<CookieHandler>())
        {
            BaseAddress = new Uri(config["Api:BaseUrl"] ?? "http://10.0.2.2:8081"),
            Timeout = TimeSpan.FromSeconds(30)
        });
        builder.Services.AddEnigmaSharedUI();

        return builder.Build();
    }
}
