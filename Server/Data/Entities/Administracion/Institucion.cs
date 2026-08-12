using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Data.Entities.Administracion;

public enum TipoInstitucion
{
  Universidad,
  Secundaria,
  Primaria,
  Curso
}

/// <summary>
/// Institución educativa (universidad, secundaria, primaria o curso) dentro del
/// despliegue único. Un usuario puede pertenecer a varias instituciones.
/// </summary>
public class Institucion : GenericEntity
{
  public string Nombre { get; set; } = null!;
  public TipoInstitucion Tipo { get; set; }

  public List<Usuario> Usuarios { get; set; } = [];
}
