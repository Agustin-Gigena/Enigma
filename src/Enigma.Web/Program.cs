using Enigma.SharedUI;
using Enigma.SharedUI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Enigma.SharedUI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient con la cookie de sesión; la BaseAddress viene de wwwroot/appsettings.json
// (ServerUri). Los servicios compartidos de la RCL consumen este HttpClient.
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<CookieHandler>())
{
    BaseAddress = new Uri(builder.Configuration["ServerUri"] ?? builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(30)
});

builder.Services.AddEnigmaSharedUI();

await builder.Build().RunAsync();
