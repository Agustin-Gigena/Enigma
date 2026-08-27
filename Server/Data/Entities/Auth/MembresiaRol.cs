namespace Enigma.Server.Data.Entities.Auth;

/// <summary>
/// Asignación de rol dentro de una membresía. Único join custom de roles: el UserRoles
/// de Identity es global (userId+roleId) y no puede scopearse por institución.
/// </summary>
public class MembresiaRol
{
    public int MembresiaId { get; set; }
    public virtual Membresia Membresia { get; set; } = null!;
    public int RolId { get; set; }
    public virtual Rol Rol { get; set; } = null!;
}
