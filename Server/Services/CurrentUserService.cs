using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Server.Services.Interfaces;

namespace Enigma.Server.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UsuarioRepository _usuarioRepository;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, UsuarioRepository usuarioRepository)                                                        
    {                                                                                                                          
       _httpContextAccessor = httpContextAccessor;                                                                            
       _usuarioRepository = usuarioRepository;
    }

    public bool IsAuthenticated()
    {
        throw new NotImplementedException();
    }

    public ClaimsPrincipal GetClaimsPrincipal()
    {
        var claimsPrincipal = _httpContextAccessor.HttpContext?.User;
        if (claimsPrincipal == null || !claimsPrincipal.Identity?.IsAuthenticated == true)
        {
            throw new UnauthorizedAccessException("El usuario no está autenticado.");
        }
        return claimsPrincipal;
    }

    public Usuario? GetCurrentUser()
    {
        var claimsPrincipal = GetClaimsPrincipal();

        var userIdClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        if (!int.TryParse(userIdClaim.Value, out int userId))
        {
            return null;
        }

        return _usuarioRepository.GetById(userId);
    }
}