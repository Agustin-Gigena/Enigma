using System.Security.Claims;
using Enigma.Api.Data.Entities.Auth;

namespace Enigma.Api.Services.Auth;

public interface ICurrentUserService
{
    public Usuario? GetCurrentUser();
    public bool IsAuthenticated();
    public ClaimsPrincipal? GetClaimsPrincipal();
}
