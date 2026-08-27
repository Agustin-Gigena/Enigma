using Enigma.Server.Data.Entities.Auth;
using Enigma.Shared.Auth;
using Enigma.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data.Repositories.Auth;

/// <summary>
/// Acceso a datos de membresías: qué secciones ve un usuario en una institución
/// (unión de los role claims "seccion" de los roles de su membresía), usuarios de
/// una institución y asignación de roles.
/// </summary>
public class MembresiaRepository : GenericRepository<Membresia>
{
    public MembresiaRepository(EnigmaDbContext context) : base(context)
    {
    }

    public Task<Membresia?> ObtenerMembresiaAsync(int usuarioId, int institucionId, CancellationToken ct = default) =>
        Context.Membresias.FirstOrDefaultAsync(m =>
            m.UsuarioId == usuarioId && m.InstitucionId == institucionId && !m.BorradoLogico, ct);

    public async Task<List<string>> ObtenerSeccionesAsync(int usuarioId, int institucionId, CancellationToken ct = default)
    {
        return await Context.Set<MembresiaRol>()
            .Where(mr => mr.Membresia.UsuarioId == usuarioId
                      && mr.Membresia.InstitucionId == institucionId
                      && !mr.Membresia.BorradoLogico
                      && !mr.Rol.BorradoLogico)
            .SelectMany(mr => Context.RoleClaims
                .Where(rc => rc.RoleId == mr.RolId && rc.ClaimType == EnigmaClaims.Seccion))
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
    }

    public async Task<List<Rol>> ObtenerRolesAsync(CancellationToken ct = default) =>
        await Context.Roles.Where(r => !r.BorradoLogico).OrderBy(r => r.Name).ToListAsync(ct);

    public async Task<List<UsuarioInstitucionDto>> ObtenerUsuariosDeInstitucionAsync(int institucionId, CancellationToken ct = default)
    {
        return await Context.Membresias
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

        List<Rol> roles = await Context.Roles
            .Where(r => !r.BorradoLogico && nombresRoles.Contains(r.Name!))
            .ToListAsync(ct);
        if (roles.Count != nombresRoles.Distinct().Count())
        {
            return false; // Algún nombre no existe.
        }

        Context.Set<MembresiaRol>().RemoveRange(
            Context.Set<MembresiaRol>().Where(mr => mr.MembresiaId == membresia.Id));
        Context.Set<MembresiaRol>().AddRange(
            roles.Select(r => new MembresiaRol { MembresiaId = membresia.Id, RolId = r.Id }));
        await Context.SaveChangesAsync(ct);
        return true;
    }
}
