using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Enigma.Test.Auth;

/// <summary>
/// WebApplicationFactory que siembra las variables de entorno que el Server lee
/// en Program (connection string MySQL + entorno) antes de que el host arranque.
/// Usa el MySQL del devcontainer (enigma-dev-db) con el seed dev admin/admin123.
/// </summary>
public sealed class EnigmaWebFactory : WebApplicationFactory<Program>
{
    public EnigmaWebFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("MYSQL_HOST", "enigma-dev-db");
        Environment.SetEnvironmentVariable("MYSQL_PORT", "3306");
        Environment.SetEnvironmentVariable("MYSQL_DATABASE", "enigma_db");
        Environment.SetEnvironmentVariable("MYSQL_USER", "root");
        Environment.SetEnvironmentVariable("MYSQL_PASSWORD", "root_password");
        Environment.SetEnvironmentVariable("MYSQL_ROOT_PASSWORD", "root_password");
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
    public async Task Login_Admin_RetornaUsuarioYDosInstituciones()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        LoginBody? response = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Usuario.NombreUsuario, Is.EqualTo("admin"));
        Assert.That(response.Instituciones, Is.Not.Null);
        Assert.That(response.Instituciones.Count, Is.EqualTo(2));

        // Verify cookie was set
        Assert.That(login.Headers.Contains("Set-Cookie"), Is.True,
            "Login response should set enigma_token cookie");
        string? cookieHeader = string.Join(",", login.Headers.GetValues("Set-Cookie"));
        Assert.That(cookieHeader, Does.Contain("enigma_token"));
    }

    [Test]
    public async Task Login_CredencialesInvalidas_Retorna401()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "incorrecta"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Instituciones_ConCookie_RetornaLasMismasDos()
    {
        // Login first — cookie is stored in the CookieContainerHandler
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Request instituciones — cookie is sent automatically
        HttpResponseMessage instituciones = await _client.GetAsync("/auth/instituciones");

        Assert.That(instituciones.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        List<InstitucionDto>? lista = await instituciones.Content.ReadFromJsonAsync<List<InstitucionDto>>();
        Assert.That(lista, Is.Not.Null);
        Assert.That(lista!.Count, Is.EqualTo(2));
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

        // Attach stored cookies
        Uri uri = request.RequestUri!;
        string cookieHeader = _cookies.GetCookieHeader(uri);
        if (!string.IsNullOrEmpty(cookieHeader))
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
