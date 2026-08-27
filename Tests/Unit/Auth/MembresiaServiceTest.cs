using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Enigma.Test.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Enigma.Test.Auth;

/// <summary>Contra la BD dev real (EnigmaWebFactory). Usa prefijos para autolimpiarse.</summary>
[TestFixture]
public class MembresiaServiceTest
{
    private static EnigmaWebFactory _factory = null!;
    private static IMembresiaService _sut = null!;
    private static string _marca = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _factory = new EnigmaWebFactory();
        _sut = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IMembresiaService>();
        _marca = $"memb-test-{Guid.NewGuid():N}";

        EnigmaDbContext db = Db();
        Usuario admin = (await db.Users.FirstAsync(u => u.UserName == "admin"))!;
        var (usuario, institucion, rol) = await SembrarAsync(db, admin);
        await db.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        EnigmaDbContext db = Db();
        Membresia? m = await db.Membresias.FirstOrDefaultAsync(x => x.Usuario!.UserName == _marca);
        if (m is not null) { db.Membresias.RemoveRange(m); }
        db.Instituciones.RemoveRange(db.Instituciones.Where(i => i.Nombre.StartsWith(_marca)));
        db.Users.RemoveRange(db.Users.Where(u => u.UserName == _marca));
        db.Roles.RemoveRange(db.Roles.Where(r => r.Name!.StartsWith(_marca)));
        await db.SaveChangesAsync();
        _factory?.Dispose();
    }

    private static EnigmaDbContext Db() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();

    private static async Task<(Usuario Usuario, Institucion Institucion, Rol Rol)> SembrarAsync(EnigmaDbContext db, Usuario admin)
    {
        Usuario usuario = new() { UserName = _marca, Email = $"{_marca}@enigma.local", EmailConfirmed = true };
        Institucion institucion = new() { Nombre = $"{_marca}-inst", Tipo = TipoInstitucion.Curso };
        institucion.SetCreadoPor(admin);
        Rol rol = new($"{_marca}-rol");
        Rol rolLector = new($"{_marca}-lector");
        db.Users.Add(usuario);
        db.Instituciones.Add(institucion);
        db.Roles.AddRange(rol, rolLector);
        Membresia membresia = new() { Usuario = usuario, Institucion = institucion };
        membresia.SetCreadoPor(admin);
        membresia.Roles = [new MembresiaRol { Rol = rol }, new MembresiaRol { Rol = rolLector }];
        db.Membresias.Add(membresia);
        await db.SaveChangesAsync(); // genera ids para los RoleClaims de abajo

        // Claims de sección sobre los roles (tabla de role claims de Identity).
        db.RoleClaims.AddRange(
            new IdentityRoleClaim<int> { RoleId = rol.Id, ClaimType = EnigmaClaims.Seccion, ClaimValue = "Administracion.Usuarios" },
            new IdentityRoleClaim<int> { RoleId = rol.Id, ClaimType = EnigmaClaims.Seccion, ClaimValue = "Administracion.Instituciones" },
            new IdentityRoleClaim<int> { RoleId = rolLector.Id, ClaimType = EnigmaClaims.Seccion, ClaimValue = "Administracion.Usuarios" });
        return (usuario, institucion, rol);
    }

    [Test]
    public async Task ObtenerSeccionesAsync_UneYDedupliqueClaimsDeRoles()
    {
        EnigmaDbContext db = Db();
        Usuario usuario = (await db.Users.FirstAsync(u => u.UserName == _marca))!;
        Institucion institucion = (await db.Instituciones.FirstAsync(i => i.Nombre == $"{_marca}-inst"))!;

        List<string> secciones = await _sut.ObtenerSeccionesAsync(usuario.Id, institucion.Id);

        Assert.That(secciones, Is.EquivalentTo(new[] { "Administracion.Usuarios", "Administracion.Instituciones" }));
    }

    [Test]
    public async Task ActualizarRolesAsync_ReemplazaLosRoles()
    {
        EnigmaDbContext db = Db();
        Usuario usuario = (await db.Users.FirstAsync(u => u.UserName == _marca))!;
        Institucion institucion = (await db.Instituciones.FirstAsync(i => i.Nombre == $"{_marca}-inst"))!;
        string rolLector = $"{_marca}-lector";

        bool ok = await _sut.ActualizarRolesAsync(institucion.Id, usuario.Id, [rolLector]);
        Assert.That(ok, Is.True);
        List<string> secciones = await _sut.ObtenerSeccionesAsync(usuario.Id, institucion.Id);
        Assert.That(secciones, Is.EquivalentTo(new[] { "Administracion.Usuarios" }));

        // Volver al estado original para no acoplar tests.
        await _sut.ActualizarRolesAsync(institucion.Id, usuario.Id, [$"{_marca}-rol", rolLector]);
    }

    [Test]
    public async Task ActualizarRolesAsync_MembresiaORolInexistente_DevuelveFalse()
    {
        EnigmaDbContext db = Db();
        Usuario usuario = (await db.Users.FirstAsync(u => u.UserName == _marca))!;
        Institucion institucion = (await db.Instituciones.FirstAsync(i => i.Nombre == $"{_marca}-inst"))!;

        Assert.That(await _sut.ActualizarRolesAsync(999_999, usuario.Id, [$"{_marca}-rol"]), Is.False);
        Assert.That(await _sut.ActualizarRolesAsync(institucion.Id, usuario.Id, ["rol-inexistente"]), Is.False);
    }
}
