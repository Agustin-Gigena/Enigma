using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

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
public class LoginTest : IClassFixture<EnigmaWebFactory>
{
    private readonly HttpClient _client;

    public LoginTest(EnigmaWebFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Login_Admin_RetornaTokenYDosInstituciones()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        LoginResponse? response = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.Token));
        Assert.Equal("admin", response.Usuario.NombreUsuario);
        Assert.NotNull(response.Instituciones);
        Assert.Equal(2, response.Instituciones.Count);
    }

    [Fact]
    public async Task Login_CredencialesInvalidas_Retorna401()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "incorrecta"));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Instituciones_ConToken_RetornaLasMismasDos()
    {
        HttpResponseMessage login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin123"));
        LoginResponse? response = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(response);

        using HttpRequestMessage request = new(HttpMethod.Get, "/auth/instituciones");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", response!.Token);

        HttpResponseMessage instituciones = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, instituciones.StatusCode);
        List<InstitucionDto>? lista = await instituciones.Content.ReadFromJsonAsync<List<InstitucionDto>>();
        Assert.NotNull(lista);
        Assert.Equal(2, lista!.Count);
    }
}
