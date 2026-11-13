using Enigma.SharedUI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.SharedUI;

/// <summary>
/// Registro único de los servicios de UI compartidos. El host (Web o MAUI)
/// registra SU HttpClient (con CookieHandler y BaseAddress propia) ANTES de
/// llamar esta extensión.
/// </summary>
public static class EnigmaUIServiceCollectionExtensions
{
    public static IServiceCollection AddEnigmaSharedUI(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddAuthorizationCore();
        services.AddScoped<EnigmaAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<EnigmaAuthenticationStateProvider>());
        services.AddScoped<AuthService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<CookieHandler>();
        return services;
    }
}
