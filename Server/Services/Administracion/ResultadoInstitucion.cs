using Enigma.Shared.Dtos;

namespace Enigma.Server.Services.Administracion;

/// <summary>Resultado de crear/editar una institución: la entidad como DTO o un error mostrable.</summary>
public record ResultadoInstitucion(InstitucionDto? Institucion = null, string? Error = null)
{
    public bool Ok => Institucion is not null;
}
