using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Repositories.Administracion;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Dtos;

namespace Enigma.Server.Services.Administracion;

public interface IInstitucionService
{
    Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default);
    Task<InstitucionDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ResultadoInstitucion> CrearAsync(InstitucionRequest request, CancellationToken ct = default);
    Task<ResultadoInstitucion> ActualizarAsync(int id, InstitucionRequest request, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// ABM de instituciones (sin BD: delega en el repository, mapea entidades → DTOs).
/// Borrado lógico; el nombre debe ser único entre instituciones activas.
/// </summary>
public class InstitucionService(InstitucionRepository repository) : IInstitucionService
{
    public async Task<List<InstitucionDto>> ObtenerActivasAsync(CancellationToken ct = default)
    {
        List<Institucion> instituciones = await repository.ObtenerActivasAsync(ct);
        return instituciones.Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString())).ToList();
    }

    public async Task<InstitucionDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        Institucion? entidad = await repository.ObtenerPorIdAsync(id, ct: ct);
        return entidad is null ? null : ADto(entidad);
    }

    public async Task<ResultadoInstitucion> CrearAsync(InstitucionRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TipoInstitucion>(request.Tipo, ignoreCase: true, out TipoInstitucion tipo)
            || !Enum.IsDefined(tipo))
        {
            return new(Error: $"Tipo inválido: {request.Tipo}. Válidos: {string.Join(", ", Enum.GetNames<TipoInstitucion>())}.");
        }
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return new(Error: "El nombre es obligatorio.");
        }
        if (await repository.ObtenerPorNombreAsync(request.Nombre.Trim(), ct) is not null)
        {
            return new(Error: "Ya existe una institución activa con ese nombre.");
        }

        Institucion entidad = new() { Nombre = request.Nombre.Trim(), Tipo = tipo };
        entidad.SetCreadoPor(CurrentUserService.GetCurrentUser());
        await repository.AgregarAsync(entidad, ct);
        return new(ADto(entidad));
    }

    public async Task<ResultadoInstitucion> ActualizarAsync(int id, InstitucionRequest request, CancellationToken ct = default)
    {
        Institucion? entidad = await repository.ObtenerPorIdAsync(id, ct: ct);
        if (entidad is null)
        {
            return new(Error: "La institución no existe (o está borrada).");
        }
        if (!Enum.TryParse<TipoInstitucion>(request.Tipo, ignoreCase: true, out TipoInstitucion tipo)
            || !Enum.IsDefined(tipo))
        {
            return new(Error: $"Tipo inválido: {request.Tipo}.");
        }
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return new(Error: "El nombre es obligatorio.");
        }
        string nombre = request.Nombre.Trim();
        Institucion? homonima = await repository.ObtenerPorNombreAsync(nombre, ct);
        if (homonima is not null && homonima.Id != id)
        {
            return new(Error: "Ya existe otra institución activa con ese nombre.");
        }

        entidad.Nombre = nombre;
        entidad.Tipo = tipo;
        entidad.SetModificadoPor(CurrentUserService.GetCurrentUser());
        await repository.GuardarAsync(ct);
        return new(ADto(entidad));
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken ct = default)
    {
        Institucion? entidad = await repository.ObtenerPorIdAsync(id, ct: ct);
        if (entidad is null)
        {
            return false;
        }
        entidad.SetBorradoLogico(true, CurrentUserService.GetCurrentUser());
        await repository.GuardarAsync(ct);
        return true;
    }

    private static InstitucionDto ADto(Institucion i) => new(i.Id, i.Nombre, i.Tipo.ToString());
}
