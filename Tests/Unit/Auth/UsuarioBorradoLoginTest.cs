using System.Net;
using System.Net.Http.Json;
using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Enigma.Test.Auth;

/// <summary>
/// El login de un usuario con borrado lógico debe fallar aunque la contraseña
/// sea correcta (rama BorradoLogico de UsuarioService.LoginAsync).
/// </summary>
[TestFixture]
public class UsuarioBorradoLoginTest
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
    public async Task TearDown()
    {
        if (_factory is not null)
        {
            EnigmaDbContext db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();
            db.Usuarios.RemoveRange(db.Usuarios.Where(u => u.UserName!.StartsWith("borrado-test-")));
            await db.SaveChangesAsync();
        }
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Login_UsuarioBorradoLogico_Retorna401()
    {
        // Arrange: usuario dedicado con contraseña válida
        string nombre = $"borrado-test-{Guid.NewGuid():N}";
        UserManager<Usuario> userManager =
            _factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Usuario usuario = new() { UserName = nombre, Email = $"{nombre}@test.local", EmailConfirmed = true };
        Assert.That((await userManager.CreateAsync(usuario, "Borrado1!")).Succeeded, Is.True);

        HttpResponseMessage primero = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(nombre, "Borrado1!"));
        Assert.That(primero.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Precondición: login OK activo.");

        // Act: soft-delete directo (Usuario no es GenericEntity — ver Task 8) y reintentar
        EnigmaDbContext db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();
        Usuario tracked = (await db.Usuarios.FindAsync(usuario.Id))!;
        tracked.BorradoLogico = true;
        await db.SaveChangesAsync();

        HttpResponseMessage segundo = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(nombre, "Borrado1!"));

        // Assert
        Assert.That(segundo.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
            "Usuario borrado lógicamente no debe poder loguearse.");
        LoginBody? body = await segundo.Content.ReadFromJsonAsync<LoginBody>();
        Assert.That(body!.Usuario, Is.Null, "El 401 no debe devolver payload de login.");
    }
}
