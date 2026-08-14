using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Auth;

public interface ICurrentUserService
{
    public Usuario? GetCurrentUser();
    public bool IsAuthenticated();
    public ClaimsPrincipal? GetClaimsPrincipal();
}
