using Enigma.Client;
using Enigma.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Auth: state provider decodifica el JWT de localStorage; AuthService orquesta
// login/logout/sesión; ThemeService mantiene el modo claro/oscuro persistido.
builder.Services.AddOptions();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<EnigmaAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<EnigmaAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped(sp => new HttpClient
{
    // Front (:8080) and API (:8081) are different origins; WebAssembly cannot
    // read process env vars, so the API origin comes from wwwroot/appsettings.json.
    BaseAddress = new Uri(builder.Configuration["ServerUri"] ?? builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
