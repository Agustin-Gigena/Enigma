using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Enigma.Test.Auth;

/// <summary>
/// WebApplicationFactory que siembra defaults de las variables de entorno que el
/// Server lee en Program (connection string MySQL + entorno) antes de que el host
/// arranque. Sólo aplica el default cuando la variable no viene en el entorno:
/// en el devcontainer cae al MySQL del servicio (enigma-dev-db, seed admin/admin123);
/// en CI respeta lo que inyecta el workflow (MYSQL_HOST=localhost, etc.) — pisarlo
/// rompía la conexión a la BD y contaminaba el env de los procesos E2E hijos.
/// </summary>
public class EnigmaWebFactory : WebApplicationFactory<Program>
{
    public EnigmaWebFactory()
    {
        SetDefault("ASPNETCORE_ENVIRONMENT", "Development");
        SetDefault("MYSQL_HOST", "enigma-dev-db");
        SetDefault("MYSQL_PORT", "3306");
        SetDefault("MYSQL_DATABASE", "enigma_db");
        SetDefault("MYSQL_USER", "root");
        SetDefault("MYSQL_PASSWORD", "root_password");
        SetDefault("MYSQL_ROOT_PASSWORD", "root_password");
        SetDefault("ENIGMA_JWT_SECRET", KnownJwtSecret);
    }

    /// <summary>Secret fijo para que los tests puedan firmar JWTs válidos contra el host.
    /// SetDefault respeta un ENIGMA_JWT_SECRET heredado (CI) — usar JwtSecretParaTests para leerlo.</summary>
    public const string KnownJwtSecret = "Zb3kV9xQmN7pL2wR5tY8uH4jE6fA1sD0gC5bK3nM9oI=";

    public static string JwtSecretParaTests =>
        Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET") ?? KnownJwtSecret;

    private static void SetDefault(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
            Environment.SetEnvironmentVariable(name, value);
    }
}

/// <summary>
/// E2E del flujo de autenticación: POST /auth/login (cookie HttpOnly) y
/// GET /auth/instituciones, contra el stack real.
/// </summary>
[TestFixture]
public class LoginTest
{
    private static EnigmaWebFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new EnigmaWebFactory();
        // CreateClient with CookieContainer to handle HttpOnly cookies automatically
        _client = _factory.CreateDefaultClient(new CookieContainerHandler());
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Login_Admin_RetornaUsuarioYSusInstituciones()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        LoginBody? response = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Usuario.NombreUsuario, Is.EqualTo("admin"));
        Assert.That(response.Instituciones, Is.Not.Null);
        AssertInstitucionesDelAdmin(response.Instituciones.Select(i => i.Nombre));

        // Verify cookie was set
        Assert.That(login.Headers.Contains("Set-Cookie"), Is.True,
            "Login response should set enigma_token cookie");
        string? cookieHeader = string.Join(",", login.Headers.GetValues("Set-Cookie"));
        Assert.That(cookieHeader, Does.Contain("enigma_token"));
        Assert.That(cookieHeader, Does.Contain("httponly"), "La cookie del token debe ser HttpOnly.");
        Assert.That(cookieHeader, Does.Contain("secure"));
        Assert.That(cookieHeader, Does.Contain("samesite=none"));
        Assert.That(cookieHeader, Does.Contain("path=/"));
        Assert.That(cookieHeader, Does.Contain("max-age=28800"), "TTL de cookie = 8h (CookieMaxAgeSeconds).");
    }

    [Test]
    public async Task Login_CredencialesInvalidas_Retorna401()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "incorrecta"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("mensaje").GetString(),
            Is.EqualTo("Usuario o contraseña incorrectos."),
            "El mensaje de credenciales inválidas es contrato de la API.");
    }

    [Test]
    public async Task Instituciones_ConCookie_RetornaLasDelUsuario()
    {
        // Login first — cookie is stored in the CookieContainerHandler
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Request instituciones — cookie is sent automatically
        HttpResponseMessage instituciones = await _client.GetAsync("/auth/instituciones");

        Assert.That(instituciones.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        List<InstitucionDto>? lista = await instituciones.Content.ReadFromJsonAsync<List<InstitucionDto>>();
        Assert.That(lista, Is.Not.Null);
        AssertInstitucionesDelAdmin(lista!.Select(i => i.Nombre));
    }

    [Test]
    public async Task Me_ConCookie_RetornaUsuario()
    {
        // Login first
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Request /auth/me — cookie is sent automatically
        HttpResponseMessage me = await _client.GetAsync("/auth/me");

        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        UsuarioDto? usuario = await me.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.That(usuario, Is.Not.Null);
        Assert.That(usuario!.NombreUsuario, Is.EqualTo("admin"));
    }

    [Test]
    public async Task Logout_LimpiaCookie()
    {
        // Login first
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Logout
        HttpResponseMessage logout = await _client.PostAsync("/auth/logout", null);
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify cookie is cleared
        Assert.That(logout.Headers.Contains("Set-Cookie"), Is.True,
            "Logout response should clear enigma_token cookie");
        string? setCookie = string.Join(",", logout.Headers.GetValues("Set-Cookie"));
        Assert.That(setCookie, Does.Contain("enigma_token="));
        Assert.That(setCookie, Does.Contain("expires=Thu, 01 Jan 1970 00:00:00 GMT"),
            "Logout debe expirar la cookie en epoch.");
        Assert.That(setCookie, Does.Contain("path=/"));
        Assert.That(setCookie, Does.Contain("secure"));
        Assert.That(setCookie, Does.Contain("samesite=none"));
    }

    [Test]
    public async Task Me_ConJwtDeUsuarioInexistente_Retorna401ConMensaje()
    {
        string jwt = TokenDeUsuarioInexistente();
        using HttpRequestMessage request = new(HttpMethod.Get, "/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"enigma_token={jwt}");

        HttpResponseMessage me = await _client.SendAsync(request);

        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        JsonElement body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("mensaje").GetString(), Is.EqualTo("No autenticado."));
    }

    [Test]
    public async Task Instituciones_ConJwtDeUsuarioInexistente_Retorna401ConMensaje()
    {
        string jwt = TokenDeUsuarioInexistente();
        using HttpRequestMessage request = new(HttpMethod.Get, "/auth/instituciones");
        request.Headers.TryAddWithoutValidation("Cookie", $"enigma_token={jwt}");

        HttpResponseMessage instituciones = await _client.SendAsync(request);

        Assert.That(instituciones.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        JsonElement body = await instituciones.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("mensaje").GetString(), Is.EqualTo("No autenticado."));
    }

    private static string TokenDeUsuarioInexistente()
    {
        TokenService tokenService = new(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions { Secret = EnigmaWebFactory.JwtSecretParaTests }));
        return tokenService.GenerarAccessToken(new Usuario { Id = 999_999, UserName = "fantasma" }).Token;
    }

    /// <summary>
    /// Las instituciones del admin sembradas por el seed de dev. El endpoint debe devolver
    /// exactamente las del usuario — sin duplicados ni instituciones ajenas — sin asumir
    /// cuántas puede haber en la BD para otros usuarios.
    /// </summary>
    private static void AssertInstitucionesDelAdmin(IEnumerable<string> nombres)
    {
        string[] esperadas = ["Colegio San Martín", "Universidad Nacional del Plata"];
        Assert.That(nombres, Is.EquivalentTo(esperadas));
        Assert.That(nombres.Distinct().Count(), Is.EqualTo(nombres.Count()),
            "El usuario no debe tener instituciones duplicadas (el seed debe ser idempotente).");
    }
}

/// <summary>
/// DelegatingHandler that stores and sends cookies automatically,
/// simulating browser cookie handling for E2E tests.
/// </summary>
internal sealed class CookieContainerHandler : DelegatingHandler
{
    private readonly CookieContainer _cookies = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Version = new Version(1, 1);

        // Attach stored cookies — unless the request already carries an explicit
        // Cookie header (a test pinning its own cookies keeps full control).
        Uri uri = request.RequestUri!;
        string cookieHeader = _cookies.GetCookieHeader(uri);
        if (!string.IsNullOrEmpty(cookieHeader) && !request.Headers.Contains("Cookie"))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Store Set-Cookie headers (strip Secure flag so CookieContainer sends
        // the cookie over HTTP — required for WebApplicationFactory which uses HTTP).
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
        {
            foreach (string cookie in setCookies)
            {
                string sanitized = cookie.Replace("; Secure", "", StringComparison.OrdinalIgnoreCase);
                _cookies.SetCookies(uri, sanitized);
            }
        }

        return response;
    }
}
