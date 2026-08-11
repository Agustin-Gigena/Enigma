using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Enigma.Client.Services;

/// <summary>
/// AuthenticationStateProvider basado en el JWT guardado en localStorage:
/// decodifica los claims del payload (sin roundtrip al servidor) y notifica
/// cambios de estado tras login/logout. Un token expirado se trata como
/// anónimo.
/// </summary>
public class EnigmaAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;

    public EnigmaAuthenticationStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _js.InvokeAsync<string>("localStorage.getItem", "enigma_token");
        var claims = DecodificarClaims(token);
        if (claims.Count == 0)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotificarEstado()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static List<Claim> DecodificarClaims(string? token)
    {
        var claims = new List<Claim>();
        if (string.IsNullOrEmpty(token))
        {
            return claims;
        }

        var partes = token.Split('.');
        if (partes.Length != 3)
        {
            return claims;
        }

        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        using var documento = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        var datos = documento.RootElement;

        // Expirado → anónimo.
        if (datos.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expSegundos)
            && expSegundos < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return claims;
        }

        var id = datos.TryGetProperty("nameid", out var nameid) ? nameid.GetString()
               : datos.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
        var nombre = datos.TryGetProperty("unique_name", out var uniqueName) ? uniqueName.GetString()
                   : datos.TryGetProperty("name", out var name) ? name.GetString() : null;

        if (id is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id));
        }
        if (nombre is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, nombre));
        }
        return claims;
    }
}
