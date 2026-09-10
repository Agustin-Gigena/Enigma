using Enigma.Server.Services.Administracion;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Enigma.Server.Controllers.Administracion;

/// <summary>Sección Administracion.Instituciones: ABM completo. SIN [Authorize] —
/// la convención de namespace aplica la autorización por sección del catálogo.</summary>
[ApiController]
[Route("administracion/instituciones")]
public class InstitucionesController(IInstitucionService instituciones) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InstitucionDto>>> Get(CancellationToken ct) =>
        Ok(await instituciones.ObtenerActivasAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InstitucionDto>> GetPorId(int id, CancellationToken ct)
    {
        InstitucionDto? institucion = await instituciones.ObtenerPorIdAsync(id, ct);
        return institucion is null ? NotFound(new { mensaje = "Institución inexistente." }) : Ok(institucion);
    }

    /// <summary>Crea una institución. 400 con mensaje mostrable si el nombre se repite o el tipo no valida.</summary>
    [HttpPost]
    public async Task<ActionResult<InstitucionDto>> Crear([FromBody] InstitucionRequest request, CancellationToken ct)
    {
        ResultadoInstitucion resultado = await instituciones.CrearAsync(request, ct);
        return resultado.Ok
            ? CreatedAtAction(nameof(GetPorId), new { id = resultado.Institucion!.Id }, resultado.Institucion)
            : BadRequest(new { mensaje = resultado.Error });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InstitucionDto>> Actualizar(int id, [FromBody] InstitucionRequest request, CancellationToken ct)
    {
        ResultadoInstitucion resultado = await instituciones.ActualizarAsync(id, request, ct);
        return resultado.Ok
            ? Ok(resultado.Institucion)
            : BadRequest(new { mensaje = resultado.Error });
    }

    /// <summary>Borrado lógico. 404 si no existe.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct) =>
        await instituciones.EliminarAsync(id, ct)
            ? NoContent()
            : NotFound(new { mensaje = "Institución inexistente." });
}
