using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Services.Interfaces
{
    public interface ICurrentUserService
    {
        Usuario? GetCurrentUser();
        bool IsAuthenticated();
    }
}