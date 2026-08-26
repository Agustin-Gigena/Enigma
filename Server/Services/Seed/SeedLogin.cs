using Enigma.Server.Data.Entities.Auth;
using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Services.Seed;

/// <summary>
/// Seed del dato de login: el usuario admin (env ENIGMA_SEED_ADMIN_USER /
/// ENIGMA_SEED_ADMIN_PASSWORD, defaults admin/admin123). Idempotente: crea el
/// usuario SOLO si todavía no existe.
/// </summary>
public static class SeedLogin
{
    /// <summary>Garantiza que exista el usuario admin. Devuelve la entidad (creada o preexistente).</summary>
    public static async Task<Usuario> SeedAsync(UserManager<Usuario> userManager, ILogger logger)
    {
        string adminUserName = Environment.GetEnvironmentVariable("ENIGMA_SEED_ADMIN_USER") ?? "admin";
        string adminPassword = Environment.GetEnvironmentVariable("ENIGMA_SEED_ADMIN_PASSWORD") ?? "admin123";

        Usuario? admin = await userManager.FindByNameAsync(adminUserName);
        if (admin is not null)
        {
            return admin;
        }

        admin = new Usuario
        {
            UserName = adminUserName,
            Email = $"{adminUserName}@enigma.local",
            EmailConfirmed = true,
        };
        IdentityResult crear = await userManager.CreateAsync(admin, adminPassword);
        if (!crear.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo crear el usuario admin de seed: "
                + string.Join("; ", crear.Errors.Select(e => e.Description)));
        }
        logger.LogInformation("Seed: creado usuario {Admin}", adminUserName);
        return admin;
    }
}
