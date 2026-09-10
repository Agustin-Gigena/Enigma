using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Services.Administracion;

public interface IUsuariosAdministracionService
{
    Task<ResultadoAltaUsuario> AltaEnInstitucionAsync(int institucionId, AltaUsuarioRequest request, CancellationToken ct = default);
    Task<bool> QuitarDeInstitucionAsync(int institucionId, int usuarioId, CancellationToken ct = default);
}

/// <summary>
/// ABM de usuarios DENTRO de la institución activa: alta con contraseña inicial
/// (Identity) + membresía con roles; baja = borrado lógico de la membresía (el
/// usuario puede seguir en otras instituciones). Sin DbContext directo:
/// UserManager/RoleManager (Identity) + repositories.
/// </summary>
public class UsuariosAdministracionService(
    UserManager<Usuario> userManager,
    MembresiaRepository membresias) : IUsuariosAdministracionService
{
    public async Task<ResultadoAltaUsuario> AltaEnInstitucionAsync(int institucionId, AltaUsuarioRequest request, CancellationToken ct = default)
    {
        List<string> errores = [];

        if (string.IsNullOrWhiteSpace(request.NombreUsuario))
        {
            errores.Add("El usuario es obligatorio.");
        }
        if (await userManager.FindByNameAsync(request.NombreUsuario.Trim()) is not null)
        {
            errores.Add("Ya existe un usuario con ese nombre.");
        }
        if (request.Correo is { Length: > 0 } && await userManager.FindByEmailAsync(request.Correo.Trim()) is not null)
        {
            errores.Add("Ya existe un usuario con ese correo.");
        }
        if (errores.Count > 0)
        {
            return new(Errores: errores);
        }

        Usuario usuario = new()
        {
            UserName = request.NombreUsuario.Trim(),
            Email = request.Correo?.Trim(),
            EmailConfirmed = true,
        };
        IdentityResult creado = await userManager.CreateAsync(usuario, request.Contrasena);
        if (!creado.Succeeded)
        {
            return new(Errores: [.. creado.Errors.Select(e => e.Description)]);
        }

        Membresia membresia = new() { UsuarioId = usuario.Id, InstitucionId = institucionId };
        membresia.SetCreadoPor(CurrentUserService.GetCurrentUser());
        await membresias.AgregarAsync(membresia, ct);

        if (request.Roles.Count > 0)
        {
            await membresias.ActualizarRolesAsync(institucionId, usuario.Id, request.Roles, ct);
        }

        return new(new UsuarioInstitucionDto(
            new UsuarioDto(usuario.Id, usuario.UserName!, usuario.Email),
            [.. request.Roles]));
    }

    public async Task<bool> QuitarDeInstitucionAsync(int institucionId, int usuarioId, CancellationToken ct = default)
    {
        Membresia? membresia = await membresias.ObtenerMembresiaAsync(usuarioId, institucionId, ct);
        if (membresia is null)
        {
            return false;
        }
        membresia.SetBorradoLogico(true, CurrentUserService.GetCurrentUser());
        await membresias.GuardarAsync(ct);
        return true;
    }
}
