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

    public Task<Institucion?> ObtenerPorIdAsync(int id, bool incluirBorradas = false, CancellationToken ct = default) =>
        context.Instituciones.FirstOrDefaultAsync(i => i.Id == id && (incluirBorradas || !i.BorradoLogico), ct);

    public Task<Institucion?> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default) =>
        context.Instituciones.FirstOrDefaultAsync(i => i.Nombre == nombre && !i.BorradoLogico, ct);

    public async Task<int> AgregarAsync(Institucion institucion, CancellationToken ct = default)
    {
        context.Instituciones.Add(institucion);
        await context.SaveChangesAsync(ct); // materializa el Id: la membresía referencia la FK real.
        return institucion.Id;
    }

    public Task GuardarAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}

