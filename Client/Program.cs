using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Enigma.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    // Front (:80) and API (:8081) are different origins; WebAssembly cannot
    // read process env vars, so the API origin comes from wwwroot/appsettings.json.
    BaseAddress = new Uri(builder.Configuration["ServerUri"] ?? builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
