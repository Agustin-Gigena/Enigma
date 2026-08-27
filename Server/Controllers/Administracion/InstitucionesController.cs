using Enigma.Server.Services.Administracion;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Enigma.Server.Controllers.Administracion;

/// <summary>Sección Administracion.Instituciones: SIN [Authorize] — la convención de
/// namespace aplica la autorización por sección del catálogo.</summary>
[ApiController]
[Route("administracion/instituciones")]
public class InstitucionesController(IInstitucionService instituciones) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InstitucionDto>>> Get(CancellationToken ct) =>
        Ok(await instituciones.ObtenerActivasAsync(ct));
}
