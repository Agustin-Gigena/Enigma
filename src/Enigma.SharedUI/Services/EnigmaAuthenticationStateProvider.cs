using System.Net.Http.Json;
using System.Security.Claims;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Enigma.SharedUI.Services;

/// <summary>
/// AuthenticationStateProvider basado en cookie HttpOnly:
/// llama a GET /auth/me bajo demanda (no en cada carga de página) y cachea
/// el resultado. Se invalida en logout o cuando el server devuelve 401.
/// </summary>
public class EnigmaAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    private AuthenticationState? _cachedState;
    private DateTime _cacheExpiry = DateTime.MinValue;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public EnigmaAuthenticationStateProvider(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cachedState is not null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedState;
        }

        try
        {
            string json = await _js.InvokeAsync<string>("localStorage.getItem", "enigma_usuario");
            if (string.IsNullOrEmpty(json))
            {
                _cachedState = Anonymous();
                return _cachedState;
            }

            // Timeout corto: sin esto, un host inalcanzable (app MAUI en
            // dispositivo/red sin ruta a la API) cuelga el AuthorizeRouteView
            // ~100s (timeout default de HttpClient) y la UI queda "autorizando".
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
            HttpResponseMessage response = await _http.GetAsync("auth/me", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                UsuarioDto? usuario = await response.Content.ReadFromJsonAsync<UsuarioDto>(cts.Token);
                if (usuario is not null)
                {
                    ClaimsPrincipal principal = CreatePrincipal(usuario);
                    _cachedState = new AuthenticationState(principal);
                    _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                    return _cachedState;
                }
            }
        }
        // Catch amplio apropósito: cualquier fallo (red, TLS bloqueado, JS
        // interop no disponible en el arranque de Blazor Hybrid) resuelve
        // anónimo y manda al Login en vez de colgar el Router.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JSException or InvalidOperationException)
        {
        }

        _cachedState = Anonymous();
        return _cachedState;
    }

    public void NotifyAuthStateChanged()
    {
        _cachedState = null;
        _cacheExpiry = DateTime.MinValue;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyLogout()
    {
        _cachedState = null;
        _cacheExpiry = DateTime.MinValue;
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static ClaimsPrincipal CreatePrincipal(UsuarioDto usuario)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreUsuario),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "cookie"));
    }
}
