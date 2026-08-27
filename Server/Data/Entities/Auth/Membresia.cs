using Enigma.Server.Data.Entities.Administracion;

namespace Enigma.Server.Data.Entities.Auth;

/// <summary>
/// Membresía de un usuario en una institución. Reemplaza la N:M implícita para poder
/// colgar roles por institución (MembresiaRol) y auditar el vínculo.
/// </summary>
public class Membresia : GenericEntity
{
    public int UsuarioId { get; set; }
    public virtual Usuario Usuario { get; set; } = null!;
    public int InstitucionId { get; set; }
    public virtual Institucion Institucion { get; set; } = null!;
    public virtual List<MembresiaRol> Roles { get; set; } = [];
}
