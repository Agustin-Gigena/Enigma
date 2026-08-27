using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Data.Entities.Auth;

/// <summary>
/// Rol con las secciones habilitadas como role claims de Identity (claim "seccion",
/// clave = Modulo.Seccion del catálogo). Como Usuario, hereda de Identity y replica
/// los sellos de auditoría sin las navegaciones hacia Usuario (relación circular).
/// </summary>
public class Rol : IdentityRole<int>
{
    public Rol() { }

    public Rol(string nombre) : base(nombre) { }

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ModificadoEn { get; set; }
    public DateTime? BorradoEn { get; set; }
    public bool BorradoLogico { get; set; } = false;
}
