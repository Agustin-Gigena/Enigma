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

    public EnigmaAuthenticationStateProvider(IJSRuntime js) => _js = js;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string token = await _js.InvokeAsync<string>("localStorage.getItem", "enigma_token");
        List<Claim> claims = DecodificarClaims(token);
        if (claims.Count == 0)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        ClaimsIdentity identity = new(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotificarEstado() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static List<Claim> DecodificarClaims(string? token)
    {
        List<Claim> claims = new();
        if (string.IsNullOrEmpty(token))
        {
            return claims;
        }

        string[] partes = token.Split('.');
        if (partes.Length != 3)
        {
            return claims;
        }

        string payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        using JsonDocument documento = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        JsonElement datos = documento.RootElement;

        // Expirado → anónimo.
        if (datos.TryGetProperty("exp", out JsonElement exp) && exp.TryGetInt64(out long expSegundos)
            && expSegundos < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return claims;
        }

        string? id = datos.TryGetProperty("nameid", out JsonElement nameid) ? nameid.GetString()
               : datos.TryGetProperty("sub", out JsonElement sub) ? sub.GetString() : null;
        string? nombre = datos.TryGetProperty("unique_name", out JsonElement uniqueName) ? uniqueName.GetString()
                   : datos.TryGetProperty("name", out JsonElement name) ? name.GetString() : null;

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
