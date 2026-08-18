using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
/// E2E del flujo de autenticación: POST /auth/login (Identity + JWT) y
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
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task Login_Admin_RetornaTokenYDosInstituciones()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        LoginResponse? response = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(response, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(response!.Token), Is.False);
        Assert.That(response.Usuario.NombreUsuario, Is.EqualTo("admin"));
        Assert.That(response.Instituciones, Is.Not.Null);
        Assert.That(response.Instituciones.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Login_CredencialesInvalidas_Retorna401()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "incorrecta"));

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Instituciones_ConToken_RetornaLasMismasDos()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        LoginResponse? response = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(response, Is.Not.Null);

        using HttpRequestMessage request = new(HttpMethod.Get, "/auth/instituciones");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", response!.Token);

        HttpResponseMessage instituciones = await _client.SendAsync(request);

        Assert.That(instituciones.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        List<InstitucionDto>? lista = await instituciones.Content.ReadFromJsonAsync<List<InstitucionDto>>();
        Assert.That(lista, Is.Not.Null);
        Assert.That(lista!.Count, Is.EqualTo(2));
    }
}
