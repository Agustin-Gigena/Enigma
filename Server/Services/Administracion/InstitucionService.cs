using Enigma.Server.Data.Repositories.Administracion;
using Enigma.Shared.Dtos;

namespace Enigma.Server.Services.Administracion;

public interface IInstitucionService
{
    Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default);
}

/// <summary>Catálogo de instituciones activas (sin BD: delega en el repository).</summary>
public class InstitucionService(InstitucionRepository repository) : IInstitucionService
{
    public Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default) =>
        repository.ObtenerActivasAsync(ct);
}
