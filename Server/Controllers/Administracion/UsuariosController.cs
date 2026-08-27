using Enigma.Server.Services.Auth;
using Enigma.Shared.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Enigma.Server.Controllers.Administracion;

/// <summary>Sección Administracion.Usuarios: usuarios de la institución activa (claim
/// "institucion" del token) y asignación de roles de su membresía. SIN [Authorize]
/// manual — la convención de namespace exige sesión con la sección del catálogo.</summary>
[ApiController]
[Route("administracion/usuarios")]
public class UsuariosController(IMembresiaService membresia) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UsuarioInstitucionDto>>> Get(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst(EnigmaClaims.Institucion)?.Value, out int institucionId))
        {
            return Forbid();
        }
        return Ok(await membresia.ObtenerUsuariosDeInstitucionAsync(institucionId, ct));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RolDto>>> Roles(CancellationToken ct) =>
        Ok((await membresia.ObtenerRolesAsync(ct))
            .Select(r => new RolDto(r.Id, r.Name!)).ToList());

    [HttpPut("{usuarioId}/roles")]
    public async Task<IActionResult> ActualizarRoles(int usuarioId, [FromBody] ActualizarRolesRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst(EnigmaClaims.Institucion)?.Value, out int institucionId))
        {
            return Forbid();
        }

        bool ok = await membresia.ActualizarRolesAsync(institucionId, usuarioId, request.Roles, ct);
        return ok ? NoContent() : NotFound(new { mensaje = "Membresía inexistente o rol desconocido." });
    }
}
