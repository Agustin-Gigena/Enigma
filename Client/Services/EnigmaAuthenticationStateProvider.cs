using System.Net.Http.Json;
using System.Security.Claims;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Enigma.Client.Services;

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

        string json = await _js.InvokeAsync<string>("localStorage.getItem", "enigma_usuario");
        if (string.IsNullOrEmpty(json))
        {
            _cachedState = Anonymous();
            return _cachedState;
        }

        try
        {
            HttpResponseMessage response = await _http.GetAsync("auth/me");
            if (response.IsSuccessStatusCode)
            {
                SesionDto? sesion = await response.Content.ReadFromJsonAsync<SesionDto>();
                if (sesion is not null)
                {
                    ClaimsPrincipal principal = CreatePrincipal(sesion);
                    _cachedState = new AuthenticationState(principal);
                    _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                    return _cachedState;
                }
            }
        }
        catch (HttpRequestException)
        {
        }

        _cachedState = Anonymous();
        return _cachedState;
    }

    /// <summary>Id de institución activa según el token (null si aún no eligió).</summary>
    public async Task<int?> GetInstitucionActivaIdAsync()
    {
        AuthenticationState estado = await GetAuthenticationStateAsync();
        string? valor = estado.User.FindFirst("institucion")?.Value;
        return int.TryParse(valor, out int id) ? id : null;
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

    private static ClaimsPrincipal CreatePrincipal(SesionDto sesion)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, sesion.Usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, sesion.Usuario.NombreUsuario),
        ];
        claims.AddRange(sesion.Permisos.Select(p => new Claim("permiso", p)));
        if (sesion.InstitucionActivaId is int institucion)
        {
            claims.Add(new Claim("institucion", institucion.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "cookie"));
    }
}
