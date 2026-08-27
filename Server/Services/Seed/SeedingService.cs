using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Shared.Auth;
using Enigma.Shared.Modules;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Services.Seed;

/// <summary>
/// Seed de desarrollo: usuario admin (vía <see cref="SeedLogin"/>), rol Administrador
/// con una claim de sección por sección del catálogo, y dos instituciones de ejemplo
/// con membresía del admin. Idempotente: cada dato se crea solo si no existe, así que
/// re-ejecutar (o que un test la invoque sobre una BD ya sembrada) no duplica filas.
/// </summary>
public static class SeedingService
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using IServiceScope scope = services.CreateScope();
        UserManager<Usuario> userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        EnigmaDbContext db = scope.ServiceProvider.GetRequiredService<EnigmaDbContext>();

        Usuario admin = await SeedLogin.SeedAsync(userManager, logger);
        Rol administrador = await EnsureRolAdministradorAsync(db);

        await EnsureInstitucionConMembresiaAsync(db, admin, administrador, "Universidad Nacional del Plata", TipoInstitucion.Universidad);
        await EnsureInstitucionConMembresiaAsync(db, admin, administrador, "Colegio San Martín", TipoInstitucion.Secundaria);
    }

    /// <summary>
    /// Rol "Administrador" con una claim de sección por sección del catálogo: es lo que
    /// habilita los permisos del admin sembrado al elegir institución. Idempotente en claims.
    /// </summary>
    private static async Task<Rol> EnsureRolAdministradorAsync(EnigmaDbContext db)
    {
        Rol? rol = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrador");
        if (rol is null)
        {
            rol = new Rol("Administrador");
            db.Roles.Add(rol);
            await db.SaveChangesAsync(); // materializa el Id para las claims de sección.
        }

        List<string> existentes = await db.RoleClaims
            .Where(rc => rc.RoleId == rol.Id && rc.ClaimType == EnigmaClaims.Seccion)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync();
        db.RoleClaims.AddRange(
            CatalogoModulos.Secciones
                .Select(s => s.Clave)
                .Where(clave => !existentes.Contains(clave))
                .Select(clave => new IdentityRoleClaim<int> { RoleId = rol.Id, ClaimType = EnigmaClaims.Seccion, ClaimValue = clave }));
        await db.SaveChangesAsync();
        return rol;
    }

    /// <summary>Crea la institución (si no existe por nombre) y la vincula al admin (si no está ya vinculada).</summary>
    private static async Task EnsureInstitucionConMembresiaAsync(
        EnigmaDbContext db, Usuario admin, Rol administrador, string nombre, TipoInstitucion tipo)
    {
        Institucion? institucion = await db.Instituciones.FirstOrDefaultAsync(i => i.Nombre == nombre);
        if (institucion is null)
        {
            institucion = new Institucion { Nombre = nombre, Tipo = tipo };
            institucion.SetCreadoPor(admin);
            db.Instituciones.Add(institucion);
            await db.SaveChangesAsync(); // materializa el Id: la membresía referencia la FK real.
        }

        Membresia? membresia = await db.Membresias.FirstOrDefaultAsync(m =>
            m.UsuarioId == admin.Id && m.InstitucionId == institucion.Id && !m.BorradoLogico);
        if (membresia is null)
        {
            membresia = new Membresia { UsuarioId = admin.Id, InstitucionId = institucion.Id };
            membresia.SetCreadoPor(admin);
            db.Membresias.Add(membresia);
            await db.SaveChangesAsync(); // materializa el Id para el vínculo con el rol.
        }

        bool tieneRol = await db.Set<MembresiaRol>()
            .AnyAsync(mr => mr.MembresiaId == membresia.Id && mr.RolId == administrador.Id);
        if (!tieneRol)
        {
            db.Set<MembresiaRol>().Add(new MembresiaRol { MembresiaId = membresia.Id, RolId = administrador.Id });
        }

        await db.SaveChangesAsync();
    }
}
