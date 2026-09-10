using Enigma.Server.Services.Administracion;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Enigma.Server.Controllers.Administracion;

/// <summary>Sección Administracion.Usuarios: ABM de los usuarios de la institución
/// activa (claim "institucion" del token): listar, alta con contraseña inicial,
/// roles de su membresía y baja (borrado lógico de la membresía). SIN [Authorize]
/// manual — la convención de namespace exige sesión con la sección del catálogo.</summary>
[ApiController]
[Route("administracion/usuarios")]
public class UsuariosController(
    IMembresiaService membresia,
    IUsuariosAdministracionService administracion) : ControllerBase
{
    private int? InstitucionActiva
    {
        get
        {
            string? valor = User.FindFirst(EnigmaClaims.Institucion)?.Value;
            return int.TryParse(valor, out int id) ? id : null;
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioInstitucionDto>>> Get(CancellationToken ct)
    {
        int? institucionId = InstitucionActiva;
        return institucionId is null
            ? Forbid()
            : Ok(await membresia.ObtenerUsuariosDeInstitucionAsync(institucionId.Value, ct));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RolDto>>> Roles(CancellationToken ct) =>
        Ok((await membresia.ObtenerRolesAsync(ct))
            .Select(r => new RolDto(r.Id, r.Name!)).ToList());

    /// <summary>Alta de usuario en la institución activa: Identity + membresía + roles.
    /// 400 con errores por campo (nombre repetido, política de contraseña…).</summary>
    [HttpPost]
    public async Task<ActionResult<UsuarioInstitucionDto>> Alta([FromBody] AltaUsuarioRequest request, CancellationToken ct)
    {
        int? institucionId = InstitucionActiva;
        if (institucionId is null)
        {
            return Forbid();
        }

        ResultadoAltaUsuario resultado = await administracion.AltaEnInstitucionAsync(institucionId.Value, request, ct);
        return resultado.Ok
            ? StatusCode(StatusCodes.Status201Created, resultado.Usuario)
            : BadRequest(new { mensaje = string.Join(" ", resultado.Errores) });
    }

    [HttpPut("{usuarioId}/roles")]
    public async Task<IActionResult> ActualizarRoles(int usuarioId, [FromBody] ActualizarRolesRequest request, CancellationToken ct)
    {
        int? institucionId = InstitucionActiva;
        if (institucionId is null)
        {
            return Forbid();
        }

        bool ok = await membresia.ActualizarRolesAsync(institucionId.Value, usuarioId, request.Roles, ct);
        return ok ? NoContent() : NotFound(new { mensaje = "Membresía inexistente o rol desconocido." });
    }

    /// <summary>Baja de la institución activa: borrado lógico de la membresía (el
    /// usuario conserva sus otras instituciones). 404 si no es miembro.</summary>
    [HttpDelete("{usuarioId:int}")]
    public async Task<IActionResult> Quitar(int usuarioId, CancellationToken ct)
    {
        int? institucionId = InstitucionActiva;
        if (institucionId is null)
        {
            return Forbid();
        }

        return await administracion.QuitarDeInstitucionAsync(institucionId.Value, usuarioId, ct)
            ? NoContent()
            : NotFound(new { mensaje = "El usuario no es miembro de la institución." });
    }
}
