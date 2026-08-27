using Enigma.Server.Data;
using Enigma.Test.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Enigma.Test.Data;

/// <summary>Valida el modelo EF de Membresia/Rol contra la BD dev real (EnigmaWebFactory).</summary>
[TestFixture]
public class MembresiaModeloTest
{
    private static EnigmaWebFactory _factory = null!;

    [OneTimeSetUp]
    public void Setup() => _factory = new EnigmaWebFactory();

    [OneTimeTearDown]
    public void TearDown() => _factory?.Dispose();

    private static EnigmaDbContext Db() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();

    [Test]
    public void Modelo_MembresiaTieneIndiceUnicoUsuarioInstitucion()
    {
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entidad = Db().Model.FindEntityType(typeof(Enigma.Server.Data.Entities.Auth.Membresia))!;
        Assert.That(entidad, Is.Not.Null, "Membresia debe estar mapeada.");
        Assert.That(entidad.GetIndexes().Any(i => i.IsUnique && i.Properties.Count == 2), Is.True,
            "Debe existir índice único (UsuarioId, InstitucionId).");
    }

    [Test]
    public async Task Modelo_MigracionAplicaYCreaTablas()
    {
        // El boot del factory aplica migraciones; si llegamos acá, migró.
        EnigmaDbContext db = Db();
        Assert.That(await db.Membresias.CountAsync(), Is.GreaterThanOrEqualTo(0));
        Assert.That(await db.Roles.CountAsync(), Is.GreaterThanOrEqualTo(0));
    }
}
