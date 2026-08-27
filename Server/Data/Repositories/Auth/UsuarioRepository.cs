using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data.Repositories.Auth;

public class UsuarioRepository : GenericRepository<Usuario>
{
    public UsuarioRepository(EnigmaDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Instituciones activas (membresía e institución no borradas lógicamente) del usuario.
    /// </summary>
    public async Task<List<Institucion>> ObtenerInstitucionesAsync(int usuarioId, CancellationToken ct = default)
    {
        return await Context.Membresias
            .Where(m => m.UsuarioId == usuarioId && !m.BorradoLogico)
            .Select(m => m.Institucion)
            .Where(i => !i.BorradoLogico)
            .OrderBy(i => i.Nombre)
            .ToListAsync(ct);
    }

    public async Task ActualizarLastLoginAsync(int usuarioId, DateTime lastLogin, CancellationToken ct = default)
    {
        Usuario? usuario = await Context.Usuarios.FindAsync(new object[] { usuarioId }, ct);
        if (usuario is not null)
        {
            usuario.LastLogin = lastLogin;
            await Context.SaveChangesAsync(ct);
        }
    }
}
