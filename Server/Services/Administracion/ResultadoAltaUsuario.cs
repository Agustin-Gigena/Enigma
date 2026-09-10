using Enigma.Shared.Dtos;

namespace Enigma.Server.Services.Administracion;

/// <summary>Resultado del alta de usuario: el usuario creado o errores mostrables por campo.</summary>
public record ResultadoAltaUsuario(UsuarioInstitucionDto? Usuario = null, List<string> Errores = null)
{
    public bool Ok => Usuario is not null;
}
