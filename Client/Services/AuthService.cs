using System.Net.Http.Json;
using System.Text.Json;
using Enigma.Shared.Dtos;
using Microsoft.JSInterop;

namespace Enigma.Client.Services;

/// <summary>
/// Autenticación del cliente: login contra POST /auth/login, persistencia del
/// token e instituciones en localStorage, y lectura del contexto de sesión.
/// </summary>
public class AuthService
{
    private const string TokenKey = "enigma_token";
    private const string UsuarioKey = "enigma_usuario";
    private const string InstitucionesKey = "enigma_instituciones";
    private const string InstitucionActivaKey = "enigma_institucion";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly EnigmaAuthenticationStateProvider _authState;

    public AuthService(HttpClient http, IJSRuntime js, EnigmaAuthenticationStateProvider authState)
    {
        _http = http;
        _js = js;
        _authState = authState;
    }

    public async Task<LoginResult> LoginAsync(string usuario, string contrasena)
    {
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("auth/login", new { Usuario = usuario, Contrasena = contrasena });
            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult(false, "Usuario o contraseña incorrectos.");
            }

            LoginResponse? datos = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
            if (datos is null)
            {
                return new LoginResult(false, "El servidor devolvió una respuesta inválida.");
            }

            await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, datos.Token);
            await _js.InvokeVoidAsync("localStorage.setItem", UsuarioKey, JsonSerializer.Serialize(datos.Usuario, Json));
            await _js.InvokeVoidAsync("localStorage.setItem", InstitucionesKey, JsonSerializer.Serialize(datos.Instituciones, Json));
            _authState.NotificarEstado();

            return new LoginResult(true, Datos: datos);
        }
        catch (HttpRequestException)
        {
            return new LoginResult(false, "No se pudo conectar con el servidor. Intentá de nuevo.");
        }
    }

    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", UsuarioKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", InstitucionesKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", InstitucionActivaKey);
        _authState.NotificarEstado();
    }

    public async Task<UsuarioDto?> GetUsuarioAsync()
    {
        string json = await _js.InvokeAsync<string>("localStorage.getItem", UsuarioKey);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UsuarioDto>(json, Json);
    }

    public async Task<List<InstitucionDto>> GetInstitucionesAsync()
    {
        string json = await _js.InvokeAsync<string>("localStorage.getItem", InstitucionesKey);
        if (!string.IsNullOrEmpty(json))
        {
            return JsonSerializer.Deserialize<List<InstitucionDto>>(json, Json) ?? [];
        }

        // Fallback: refrescar desde la API con el token guardado.
        try
        {
            string? token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                using HttpRequestMessage request = new(HttpMethod.Get, "auth/instituciones");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    List<InstitucionDto> instituciones = await response.Content.ReadFromJsonAsync<List<InstitucionDto>>(Json) ?? [];
                    await _js.InvokeVoidAsync("localStorage.setItem", InstitucionesKey, JsonSerializer.Serialize(instituciones, Json));
                    return instituciones;
                }
            }
        }
        catch (HttpRequestException)
        {
        }
        return [];
    }

    public async Task<InstitucionDto?> GetInstitucionActivaAsync()
    {
        string json = await _js.InvokeAsync<string>("localStorage.getItem", InstitucionActivaKey);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<InstitucionDto>(json, Json);
    }

    public async Task SetInstitucionActivaAsync(InstitucionDto institucion) => await _js.InvokeVoidAsync("localStorage.setItem", InstitucionActivaKey, JsonSerializer.Serialize(institucion, Json));

    public async Task<string?> GetTokenAsync() => await _js.InvokeAsync<string>("localStorage.getItem", TokenKey);
}
