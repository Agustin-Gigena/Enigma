using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Enigma.Test.Auth;
using NUnit.Framework;

namespace Enigma.Test.E2E;

/// <summary>
/// Flujo de dos fases vía HTTP contra el server real (cookies manuales):
/// login → me responde 403 (pre-auth) → elegir institución → me responde sesión con permisos.
/// </summary>
[TestFixture]
public class FlujoSesionTest
{
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [SetUp]
    public void Setup()
    {
        // CookieContainerHandler (mismo truco que LoginTest): la cookie del server es
        // Secure y estos tests corren sobre HTTP — sin sanitizarla el contenedor
        // jamás la reenvía y todo da 401 de autenticación en vez del 403 de autorización.
        CookieContainerHandler handler = new() { InnerHandler = new HttpClientHandler() };
        _client = new HttpClient(handler) { BaseAddress = new Uri(E2EWebFixture.ServerUrl) };
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    private async Task<HttpResponseMessage> LoginAsync()
    {
        return await _client.PostAsJsonAsync("auth/login",
            new { Usuario = "admin", Contrasena = "admin123" });
    }

    [Test]
    public async Task Login_EmitePreAuth_MeRechazado()
    {
        HttpResponseMessage login = await LoginAsync();
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        HttpResponseMessage me = await _client.GetAsync("auth/me");
        Assert.That((int)me.StatusCode, Is.EqualTo(403), "El token pre-auth no debe acceder a /auth/me.");
    }

    [Test]
    public async Task ElegirInstitucion_EmiteSesionConPermisos()
    {
        await LoginAsync();
        HttpResponseMessage instituciones = await _client.GetAsync("auth/instituciones");
        // Leer como DTOs concretos.
        List<InstitucionRef>? refs = await instituciones.Content.ReadFromJsonAsync<List<InstitucionRef>>(Json);
        Assert.That(refs, Is.Not.Empty);

        HttpResponseMessage seleccion = await _client.PostAsJsonAsync("auth/institucion",
            new { InstitucionId = refs![0].Id });
        Assert.That((int)seleccion.StatusCode, Is.EqualTo(200));
        SesionRef? sesion = await seleccion.Content.ReadFromJsonAsync<SesionRef>(Json);
        Assert.That(sesion!.Permisos, Does.Contain("Administracion.Usuarios"));
        Assert.That(sesion.Permisos, Does.Contain("Administracion.Instituciones"));

        // La sesión ahora sí accede a /auth/me.
        HttpResponseMessage me = await _client.GetAsync("auth/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SesionRef? espejo = await me.Content.ReadFromJsonAsync<SesionRef>(Json);
        Assert.That(espejo!.InstitucionActivaId, Is.EqualTo(refs[0].Id));
    }

    [Test]
    public async Task ElegirInstitucion_SinMembresia_Devuelve403()
    {
        await LoginAsync();
        HttpResponseMessage seleccion = await _client.PostAsJsonAsync("auth/institucion",
            new { InstitucionId = 999_999 });
        Assert.That((int)seleccion.StatusCode, Is.EqualTo(403));
    }

    private sealed record InstitucionRef(int Id, string Nombre, string Tipo);
    private sealed record SesionRef(UsuarioRef Usuario, int? InstitucionActivaId, List<string> Permisos);
    private sealed record UsuarioRef(int Id, string NombreUsuario, string? Correo);
}
