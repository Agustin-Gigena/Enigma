using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data.Repositories.Administracion;

/// <summary>
/// Acceso a datos de instituciones (regla arquitectónica: solo los repositories
/// tocan el DbContext; los services consumen repositories).
/// </summary>
public class InstitucionRepository(EnigmaDbContext context)
{
    public async Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default) =>
        await context.Instituciones
            .Where(i => !i.BorradoLogico)
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
            .ToListAsync(ct);
}
