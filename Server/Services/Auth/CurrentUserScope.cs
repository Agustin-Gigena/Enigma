using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Auth;

/// <summary>
/// Estado ambiental del request: accessor de HttpContext + resolutor del usuario,
/// con cache por request de la entidad Usuario. Null hasta que la primera llamada
/// autenticada a GetCurrentUser() lo asigne; se reutiliza la misma instancia de Lazy
/// (y por tanto la misma Usuario) durante todo el request.
/// </summary>
internal sealed class CurrentUserScope(IHttpContextAccessor accessor, Func<int, Usuario?> resolver)
{
    public IHttpContextAccessor Accessor = accessor;
    public Func<int, Usuario?> Resolver = resolver;
    public Lazy<Usuario?>? CurrentUser;
}
