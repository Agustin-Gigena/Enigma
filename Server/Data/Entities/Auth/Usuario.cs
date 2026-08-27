using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Data.Entities.Auth;

/// <summary>
/// Usuario de la plataforma, integrado con ASP.NET Core Identity.
/// Por requisito de Identity no puede heredar <see cref="GenericEntity"/>
/// (debe heredar <see cref="IdentityUser{TKey}"/>); replica los sellos de
/// auditoría y soft-delete del patrón sin las navegaciones CreadoPor/BorradoPor
/// (la entidad de auth no se audita a sí misma con un Usuario — relación circular).
/// </summary>
public class Usuario : IdentityUser<int>
{
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ModificadoEn { get; set; }
    public DateTime? BorradoEn { get; set; }
    public bool BorradoLogico { get; set; } = false;
    public virtual List<Membresia> Membresias { get; set; } = [];
}
