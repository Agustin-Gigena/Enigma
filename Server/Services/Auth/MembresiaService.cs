using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Services.Auth;

public interface IMembresiaService
{
    Task<Membresia?> ObtenerMembresiaAsync(int usuarioId, int institucionId, CancellationToken ct = default);
    Task<List<string>> ObtenerSeccionesAsync(int usuarioId, int institucionId, CancellationToken ct = default);
    Task<List<Rol>> ObtenerRolesAsync(CancellationToken ct = default);
    Task<List<UsuarioInstitucionDto>> ObtenerUsuariosDeInstitucionAsync(int institucionId, CancellationToken ct = default);
    Task<bool> ActualizarRolesAsync(int institucionId, int usuarioId, List<string> nombresRoles, CancellationToken ct = default);
}

/// <summary>
/// Lógica de membresías: qué secciones ve un usuario en una institución (unión de los
/// role claims "seccion" de los roles de su membresía) y asignación de roles.
/// </summary>
public class MembresiaService(EnigmaDbContext context) : IMembresiaService
{
    public Task<Membresia?> ObtenerMembresiaAsync(int usuarioId, int institucionId, CancellationToken ct = default) =>
        context.Membresias.FirstOrDefaultAsync(m =>
            m.UsuarioId == usuarioId && m.InstitucionId == institucionId && !m.BorradoLogico, ct);

    public async Task<List<string>> ObtenerSeccionesAsync(int usuarioId, int institucionId, CancellationToken ct = default)
    {
        return await context.Set<MembresiaRol>()
            .Where(mr => mr.Membresia.UsuarioId == usuarioId
                      && mr.Membresia.InstitucionId == institucionId
                      && !mr.Membresia.BorradoLogico
                      && !mr.Rol.BorradoLogico)
            .SelectMany(mr => context.RoleClaims
                .Where(rc => rc.RoleId == mr.RolId && rc.ClaimType == EnigmaClaims.Seccion))
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
    }

    public async Task<List<Rol>> ObtenerRolesAsync(CancellationToken ct = default) =>
        await context.Roles.Where(r => !r.BorradoLogico).OrderBy(r => r.Name).ToListAsync(ct);

    public async Task<List<UsuarioInstitucionDto>> ObtenerUsuariosDeInstitucionAsync(int institucionId, CancellationToken ct = default)
    {
        return await context.Membresias
            .Where(m => m.InstitucionId == institucionId && !m.BorradoLogico && !m.Usuario.BorradoLogico)
            .OrderBy(m => m.Usuario.UserName)
            .Select(m => new UsuarioInstitucionDto(
                new UsuarioDto(m.UsuarioId, m.Usuario.UserName ?? "", m.Usuario.Email),
                m.Roles.Select(r => r.Rol.Name!).ToList()))
            .ToListAsync(ct);
    }

    public async Task<bool> ActualizarRolesAsync(int institucionId, int usuarioId, List<string> nombresRoles, CancellationToken ct = default)
    {
        Membresia? membresia = await ObtenerMembresiaAsync(usuarioId, institucionId, ct);
        if (membresia is null)
        {
            return false;
        }

        List<Rol> roles = await context.Roles
            .Where(r => !r.BorradoLogico && nombresRoles.Contains(r.Name!))
            .ToListAsync(ct);
        if (roles.Count != nombresRoles.Distinct().Count())
        {
            return false; // Algún nombre no existe.
        }

        context.Set<MembresiaRol>().RemoveRange(
            context.Set<MembresiaRol>().Where(mr => mr.MembresiaId == membresia.Id));
        context.Set<MembresiaRol>().AddRange(
            roles.Select(r => new MembresiaRol { MembresiaId = membresia.Id, RolId = r.Id }));
        await context.SaveChangesAsync(ct);
        return true;
    }
}
