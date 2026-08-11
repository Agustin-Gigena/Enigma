using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Auth;

public class CurrentUserService : ICurrentUserService
{
    private static readonly AsyncLocal<CurrentUserScope?> _current = new();

    /// <summary>
    /// Indica si hay un usuario autenticado en el request actual. Nunca lanza:
    /// devuelve false si no hay HttpContext o el principal es anónimo.
    /// </summary>
    public static bool IsAuthenticated()
    {
        return _current.Value?.Accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    }

    /// <summary>
    /// Devuelve el ClaimsPrincipal del request actual (lectura viva vía el accessor) o null si no hay HttpContext. Nunca lanza.
    /// </summary>
    public static ClaimsPrincipal? GetClaimsPrincipal()
    {
        return _current.Value?.Accessor.HttpContext?.User;
    }

    /// <summary>
    /// Devuelve la entidad Usuario del usuario actual, resuelta una sola vez por request
    /// (cacheada en Lazy). Si no puede producir un Usuario (sin scope, principal anónimo,
    /// claim ausente/no parseable o resolución null) devuelve null; lanza
    /// UnauthorizedAccessException si ENIGMA_AUTH_REQUIRED=true y no hay usuario.
    /// </summary>
    public static Usuario? GetCurrentUser()
    {
        Usuario? usuario = null;

        var scope = _current.Value;
        if (scope != null)
        {
            var principal = scope.Accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    scope.CurrentUser ??= new Lazy<Usuario?>(() => scope.Resolver(userId));
                    usuario = scope.CurrentUser.Value;
                }
            }
        }

        if (usuario == null && IsAuthRequired())
        {
            throw new UnauthorizedAccessException("El usuario no está autenticado.");
        }
        return usuario;
    }

    /// <summary>
    /// Política ENV: si ENIGMA_AUTH_REQUIRED=true (case-insensitive) el usuario es obligatorio.
    /// Se lee en cada llamada.
    /// </summary>
    internal static bool IsAuthRequired()
    {
        return bool.TryParse(Environment.GetEnvironmentVariable("ENIGMA_AUTH_REQUIRED"), out var b) && b;
    }

    internal static void BeginScope(IHttpContextAccessor accessor, Func<int, Usuario?> resolver)
    {
        _current.Value = new CurrentUserScope(accessor, resolver);
    }

    internal static void EndScope()
    {
        _current.Value = null;
    }

    Usuario? ICurrentUserService.GetCurrentUser() => GetCurrentUser();
    bool ICurrentUserService.IsAuthenticated() => IsAuthenticated();
    ClaimsPrincipal? ICurrentUserService.GetClaimsPrincipal() => GetClaimsPrincipal();
}

internal sealed class CurrentUserScope
{
    public CurrentUserScope(IHttpContextAccessor accessor, Func<int, Usuario?> resolver)
    {
        Accessor = accessor;
        Resolver = resolver;
    }

    public IHttpContextAccessor Accessor;
    public Func<int, Usuario?> Resolver;

    /// <summary>
    /// Cache por request de la entidad Usuario. Null hasta que la primera llamada
    /// autenticada a GetCurrentUser() lo asigne; se reutiliza la misma instancia
    /// de Lazy (y por tanto la misma Usuario) durante todo el request.
    /// </summary>
    public Lazy<Usuario?>? CurrentUser;
}
