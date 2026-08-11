using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Auth
{
    public interface ICurrentUserService
    {
        Usuario? GetCurrentUser();
        bool IsAuthenticated();
        ClaimsPrincipal? GetClaimsPrincipal();
    }
}
