using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
namespace Enigma.Server.Data;

public abstract class GenericEntity
{
    public int Id { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public virtual Usuario CreadoPor { get; set; } = null!;
    public DateTime? ModificadoEn { get; set; } = null;
    public virtual Usuario? ModificadoPor { get; set; } = null;
    public DateTime? BorradoEn { get; set; } = null;
    public virtual Usuario? BorradoPor { get; set; } = null;
    public bool BorradoLogico { get; set; } = false;

    public virtual void SetCreadoPor(Usuario? usuario = null)
    {
        if (usuario == null)
        {
            usuario = CurrentUserService.GetCurrentUser();
        }
        CreadoPor = usuario ?? throw new InvalidOperationException("No se puede establecer el usuario creador porque no hay un usuario autenticado.");
        CreadoEn = DateTime.UtcNow;
    }

    public virtual void SetModificadoPor(Usuario? usuario = null)
    {
        if (usuario == null)
        {
            usuario = CurrentUserService.GetCurrentUser();
        }
        ModificadoPor = usuario ?? throw new InvalidOperationException("No se puede establecer el usuario modificador porque no hay un usuario autenticado.");
        ModificadoEn = DateTime.UtcNow;
    }

    public virtual void SetBorradoLogico(bool borradoLogico, Usuario? usuario = null)
    {
        if (usuario == null)
        {
            usuario = CurrentUserService.GetCurrentUser();
        }

        BorradoLogico = borradoLogico;
        if (borradoLogico)
        {
            BorradoPor = usuario ?? throw new InvalidOperationException("No se puede establecer el usuario borrador porque no hay un usuario autenticado.");
            BorradoEn = DateTime.UtcNow;
        }
        else
        {
            SetModificadoPor();
            BorradoPor = null;
            BorradoEn = null;
        }
    }
}
