using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Test.Auth;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Enigma.Test.Data;

/// <summary>
/// Roundtrip de soft-delete de GenericRepository contra la BD dev real (host de
/// EnigmaWebFactory), usando Institucion (hereda GenericEntity). Usuario NO hereda
/// GenericEntity (Identity), así que su repo no persiste soft-delete: se afirma ese
/// comportamiento real aparte.
/// </summary>
[TestFixture]
public class GenericRepositoryTest
{
    /// <summary>Subclass de prueba: GenericRepository es abstracta y no existe repositorio de Institucion.</summary>
    private sealed class InstitucionRepository : GenericRepository<Institucion>
    {
        public InstitucionRepository(EnigmaDbContext context) : base(context) { }
    }

    private static EnigmaWebFactory _factory = null!;

    [OneTimeSetUp]
    public void Setup() => _factory = new EnigmaWebFactory();

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_factory is not null)
        {
            EnigmaDbContext db = Db();
            db.Instituciones.RemoveRange(db.Instituciones.Where(i => i.Nombre.StartsWith("repo-test-")));
            await db.SaveChangesAsync();
        }
        _factory?.Dispose();
    }

    private static EnigmaDbContext Db() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();

    private static InstitucionRepository Repo() => new(Db());

    [Test]
    public void GetById_Inexistente_DevuelveNull()
    {
        Assert.That(Repo().GetById(999_999), Is.Null);
    }

    [Test]
    public void SetBorradoLogico_Inexistente_DevuelveFalse()
    {
        Assert.That(Repo().SetBorradoLogico(999_999, true), Is.False);
    }

    [Test]
    public async Task SoftDelete_Roundtrip_OcultaYRestaura()
    {
        // Un solo contexto para el seed (patrón de SeedingService): el admin queda
        // tracked como Unchanged, así el Add de la institución no lo cascadea como Added.
        EnigmaDbContext db = Db();
        Usuario admin = (await db.Usuarios.FindAsync(1))!;
        Institucion entidad = new() { Nombre = $"repo-test-{Guid.NewGuid():N}", Tipo = TipoInstitucion.Curso };
        entidad.SetCreadoPor(admin);
        db.Instituciones.Add(entidad);
        await db.SaveChangesAsync();

        try
        {
            // Repo() fresco por operación: cada lectura refleja la BD real y no la
            // caché del tracker del contexto que escribió.
            Assert.That(Repo().GetById(entidad.Id), Is.Not.Null, "Institución activa visible.");

            Assert.That(Repo().SetBorradoLogico(entidad.Id, true), Is.True);
            Assert.That(Repo().GetById(entidad.Id), Is.Null, "Borrado lógico oculta por defecto.");
            Assert.That(Repo().GetById(entidad.Id, borradoLogico: true), Is.Not.Null, "Incluye borrados si se pide.");

            Assert.That(Repo().SetBorradoLogico(entidad.Id, false), Is.True);
            Assert.That(Repo().GetById(entidad.Id), Is.Not.Null, "Restaurado vuelve a ser visible.");
        }
        finally
        {
            EnigmaDbContext dbCleanup = Db();
            dbCleanup.Instituciones.RemoveRange(dbCleanup.Instituciones.Where(i => i.Nombre.StartsWith("repo-test-")));
            await dbCleanup.SaveChangesAsync();
        }
    }

    [Test]
    public async Task SetBorradoLogico_UsuarioNoEsGenericEntity_DevuelveFalseSinPersistir()
    {
        UsuarioRepository repoUsuario = _factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<UsuarioRepository>();

        Assert.That(repoUsuario.SetBorradoLogico(1, true), Is.False,
            "Usuario (Identity) no es GenericEntity: el repo genérico no persiste su soft-delete.");

        Usuario admin = (await Db().Usuarios.FindAsync(1))!;
        Assert.That(admin.BorradoLogico, Is.False, "La llamada no debe haber persistido nada.");
    }
}
