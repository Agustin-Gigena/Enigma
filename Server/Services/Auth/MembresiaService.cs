using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Shared.Dtos;

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
/// Orquestación de membresías: secciones visibles y asignación de roles. El acceso
/// a datos vive en <see cref="MembresiaRepository"/> (regla: los services no tocan
/// el contexto de EF ni los sets de entidades).
/// </summary>
public class MembresiaService : IMembresiaService
{
    private readonly MembresiaRepository _membresias;

    public MembresiaService(MembresiaRepository membresias)
    {
        _membresias = membresias;
    }

    public Task<Membresia?> ObtenerMembresiaAsync(int usuarioId, int institucionId, CancellationToken ct = default) =>
        _membresias.ObtenerMembresiaAsync(usuarioId, institucionId, ct);

    public Task<List<string>> ObtenerSeccionesAsync(int usuarioId, int institucionId, CancellationToken ct = default) =>
        _membresias.ObtenerSeccionesAsync(usuarioId, institucionId, ct);

    public Task<List<Rol>> ObtenerRolesAsync(CancellationToken ct = default) =>
        _membresias.ObtenerRolesAsync(ct);

    public Task<List<UsuarioInstitucionDto>> ObtenerUsuariosDeInstitucionAsync(int institucionId, CancellationToken ct = default) =>
        _membresias.ObtenerUsuariosDeInstitucionAsync(institucionId, ct);

    public Task<bool> ActualizarRolesAsync(int institucionId, int usuarioId, List<string> nombresRoles, CancellationToken ct = default) =>
        _membresias.ActualizarRolesAsync(institucionId, usuarioId, nombresRoles, ct);
}
