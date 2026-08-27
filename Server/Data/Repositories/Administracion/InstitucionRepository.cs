using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data.Repositories.Administracion;

/// <summary>
/// Acceso a datos de instituciones (regla arquitectónica: solo los repositories
/// tocan el DbContext y devuelven entidades; los services mapean a DTOs).
/// </summary>
public class InstitucionRepository(EnigmaDbContext context)
{
    public async Task<List<Institucion>> ObtenerActivasAsync(CancellationToken ct = default) =>
        await context.Instituciones
            .Where(i => !i.BorradoLogico)
            .OrderBy(i => i.Nombre)
            .ToListAsync(ct);

    public Task<Institucion?> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default) =>
        context.Instituciones.FirstOrDefaultAsync(i => i.Nombre == nombre, ct);

    public async Task AgregarAsync(Institucion institucion, CancellationToken ct = default)
    {
        context.Instituciones.Add(institucion);
        await context.SaveChangesAsync(ct); // materializa el Id: la membresía referencia la FK real.
    }
}
