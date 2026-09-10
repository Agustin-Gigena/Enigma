using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Auth;

/// <summary>Resultado del login con el usuario autenticado y sus instituciones.</summary>
public record LoginResultado(Usuario Usuario, List<Institucion> Instituciones);
