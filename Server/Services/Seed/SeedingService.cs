using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Services.Seed;

/// <summary>
/// Seed de desarrollo: usuario admin (vía <see cref="SeedLogin"/>) y dos
/// instituciones de ejemplo con membresía del admin. Idempotente: cada dato se
/// crea solo si no existe, así que re-ejecutar (o que un test la invoque sobre
/// una BD ya sembrada) no duplica filas.
/// </summary>
public static class SeedingService
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using IServiceScope scope = services.CreateScope();
        UserManager<Usuario> userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        EnigmaDbContext db = scope.ServiceProvider.GetRequiredService<EnigmaDbContext>();

        Usuario admin = await SeedLogin.SeedAsync(userManager, logger);
        await db.Entry(admin).Collection(u => u.Instituciones).LoadAsync();

        await EnsureInstitucionConMembresiaAsync(db, admin, "Universidad Nacional del Plata", TipoInstitucion.Universidad);
        await EnsureInstitucionConMembresiaAsync(db, admin, "Colegio San Martín", TipoInstitucion.Secundaria);
    }

    /// <summary>Crea la institución (si no existe por nombre) y la vincula al admin (si no está ya vinculada).</summary>
    private static async Task EnsureInstitucionConMembresiaAsync(
        EnigmaDbContext db, Usuario admin, string nombre, TipoInstitucion tipo)
    {
        Institucion? institucion = await db.Instituciones.FirstOrDefaultAsync(i => i.Nombre == nombre);
        if (institucion is null)
        {
            institucion = new Institucion { Nombre = nombre, Tipo = tipo };
            institucion.SetCreadoPor(admin);
            db.Instituciones.Add(institucion);
        }

        if (admin.Instituciones.All(i => i.Nombre != nombre))
        {
            admin.Instituciones.Add(institucion);
        }

        await db.SaveChangesAsync();
    }
}
