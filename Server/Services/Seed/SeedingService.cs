using System.Security.Claims;

using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Administracion;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Shared.Auth;
using Enigma.Shared.Modules;
using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Services.Seed;

/// <summary>
/// Seed de desarrollo: usuario admin (vía <see cref="SeedLogin"/>), rol Admin con una
/// claim de sección por sección del catálogo, y dos instituciones de ejemplo con
/// membresía del admin. Idempotente: cada dato se crea solo si no existe y las claims
/// nuevas del catálogo se agregan al re-sembrar, así que re-ejecutar (o que un test la
/// invoque sobre una BD ya sembrada) no duplica filas ni deja permisos desactualizados.
/// </summary>
public static class SeedingService
{
    private const string RolAdmin = "Admin";

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using IServiceScope scope = services.CreateScope();
        UserManager<Usuario> userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        RoleManager<Rol> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();
        InstitucionRepository instituciones = scope.ServiceProvider.GetRequiredService<InstitucionRepository>();
        MembresiaRepository membresias = scope.ServiceProvider.GetRequiredService<MembresiaRepository>();

        Usuario admin = await SeedLogin.SeedAsync(userManager, logger);
        await EnsureRolAdminAsync(roleManager);

        await EnsureInstitucionConMembresiaAsync(instituciones, membresias, admin, "Universidad Nacional del Plata", TipoInstitucion.Universidad);
        await EnsureInstitucionConMembresiaAsync(instituciones, membresias, admin, "Colegio San Martín", TipoInstitucion.Secundaria);

        // Al final (las membresías ya existen, incluso las creadas fuera del seed):
        // el rol Admin alcanza a TODAS las membresías activas del usuario admin.
        await EnsureRolEnMembresiasAsync(membresias, roleManager, admin);
    }

    /// <summary>
    /// Rol Admin con una claim "seccion" por cada sección del catálogo: es lo que
    /// habilita los permisos del admin sembrado al elegir institución. Nunca queda
    /// desactualizado: las secciones nuevas del catálogo se agregan solas al re-sembrar.
    /// </summary>
    private static async Task EnsureRolAdminAsync(RoleManager<Rol> roleManager)
    {
        Rol? rol = await roleManager.FindByNameAsync(RolAdmin);
        if (rol is null)
        {
            rol = new Rol(RolAdmin);
            IdentityResult creado = await roleManager.CreateAsync(rol);
            if (!creado.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo crear el rol Admin: " + string.Join("; ", creado.Errors.Select(e => e.Description)));
            }
        }

        IList<Claim> existentes = await roleManager.GetClaimsAsync(rol);
        foreach (string seccion in CatalogoModulos.Secciones.Select(s => s.Clave))
        {
            if (!existentes.Any(c => c.Type == EnigmaClaims.Seccion && c.Value == seccion))
            {
                await roleManager.AddClaimAsync(rol, new Claim(EnigmaClaims.Seccion, seccion));
            }
        }
    }

    /// <summary>Crea la institución (si no existe por nombre) y la vincula al admin (si no está ya vinculada).</summary>
    private static async Task EnsureInstitucionConMembresiaAsync(
        InstitucionRepository instituciones, MembresiaRepository membresias, Usuario admin, string nombre, TipoInstitucion tipo)
    {
        Institucion? institucion = await instituciones.ObtenerPorNombreAsync(nombre);
        if (institucion is null)
        {
            institucion = new Institucion { Nombre = nombre, Tipo = tipo };
            institucion.SetCreadoPor(admin);
            await instituciones.AgregarAsync(institucion); // materializa el Id: la membresía referencia la FK real.
        }

        Membresia? membresia = await membresias.ObtenerMembresiaAsync(admin.Id, institucion.Id);
        if (membresia is null)
        {
            membresia = new Membresia { UsuarioId = admin.Id, InstitucionId = institucion.Id };
            membresia.SetCreadoPor(admin);
            await membresias.AgregarAsync(membresia); // materializa el Id para el vínculo con el rol.
        }
    }

    /// <summary>Asigna el rol Admin a las membresías activas del admin que aún no lo tienen.</summary>
    private static async Task EnsureRolEnMembresiasAsync(
        MembresiaRepository membresias, RoleManager<Rol> roleManager, Usuario admin)
    {
        Rol rolAdmin = (await roleManager.FindByNameAsync(RolAdmin))!;
        List<Membresia> delAdmin = await membresias.ObtenerPorUsuarioAsync(admin.Id);
        foreach (Membresia membresia in delAdmin.Where(m => m.Roles.All(r => r.RolId != rolAdmin.Id)))
        {
            await membresias.AsignarRolAsync(membresia.Id, rolAdmin.Id);
        }
    }
}
