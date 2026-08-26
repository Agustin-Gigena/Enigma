using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Seed;
using Enigma.Test.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Enigma.Test.Seed;

public class SeedingServiceTest
{
    private static EnigmaWebFactory _factory = null!;

    [OneTimeSetUp]
    public void Setup() => _factory = new EnigmaWebFactory();

    [OneTimeTearDown]
    public void TearDown() => _factory?.Dispose();

    [Test]
    public async Task SeedAsync_DobleEjecucion_NoDuplicaNiLanza()
    {
        await SeedingService.SeedAsync(_factory.Services, NullLogger.Instance);
        await SeedingService.SeedAsync(_factory.Services, NullLogger.Instance);

        EnigmaDbContext db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();
        Assert.That(await db.Instituciones.CountAsync(i => i.Nombre == "Universidad Nacional del Plata"), Is.EqualTo(1));
        Assert.That(await db.Instituciones.CountAsync(i => i.Nombre == "Colegio San Martín"), Is.EqualTo(1));

        UserManager<Usuario> userManager =
            _factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Usuario admin = (await userManager.FindByNameAsync("admin"))!;
        Assert.That(admin, Is.Not.Null);
        await db.Entry(admin).Collection(u => u.Instituciones).LoadAsync();
        Assert.That(admin.Instituciones.Select(i => i.Nombre).Distinct().Count(), Is.EqualTo(2));
        Assert.That(admin.Instituciones.Count, Is.EqualTo(2), "Sin membresías duplicadas.");
    }

    [Test]
    public async Task SeedAsync_BDVacia_CreaAdminEInstituciones()
    {
        await using SeedDatabaseFactory factory = new();
        using HttpClient _ = factory.CreateClient(); // fuerza boot: migraciones + seed

        EnigmaDbContext db = factory.Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();
        Assert.That(await db.Instituciones.CountAsync(), Is.EqualTo(2));

        UserManager<Usuario> userManager =
            factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Usuario admin = (await userManager.FindByNameAsync("admin"))!;
        Assert.That(admin, Is.Not.Null, "El seed debe crear el admin en BD vacía.");

        await db.Entry(admin).Collection(u => u.Instituciones).LoadAsync();
        Assert.That(admin.Instituciones.Select(i => i.Nombre).Distinct().Count(), Is.EqualTo(2), "Las 2 instituciones deben quedar vinculadas al admin.");
        Assert.That(admin.Instituciones.Count, Is.EqualTo(2), "Sin membresías duplicadas.");
    }
}

/// <summary>Factory con BD dedicada enigma_seed_test: el host migra y siembra desde cero.
/// Teardown borra la BD y restaura el entorno heredado.</summary>
internal sealed class SeedDatabaseFactory : EnigmaWebFactory, IAsyncDisposable
{
    private readonly string? _original;

    public SeedDatabaseFactory()
    {
        _original = Environment.GetEnvironmentVariable("MYSQL_DATABASE");
        Environment.SetEnvironmentVariable("MYSQL_DATABASE", "enigma_seed_test");
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            EnigmaDbContext db = Services.CreateScope().ServiceProvider.GetRequiredService<EnigmaDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYSQL_DATABASE", _original);
            await base.DisposeAsync();
        }
    }
}
