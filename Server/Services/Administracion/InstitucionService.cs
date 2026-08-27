using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Repositories.Administracion;
using Enigma.Shared.Dtos;

namespace Enigma.Server.Services.Administracion;

public interface IInstitucionService
{
    Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default);
}

/// <summary>Catálogo de instituciones activas (sin BD: delega en el repository y
/// mapea entidades → DTOs, como manda la regla arquitectónica).</summary>
public class InstitucionService(InstitucionRepository repository) : IInstitucionService
{
    public async Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default)
    {
        List<Institucion> instituciones = await repository.ObtenerActivasAsync(ct);
        return instituciones.Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString())).ToList();
    }
}
