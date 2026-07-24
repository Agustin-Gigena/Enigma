using Enigma.Server.Data.Entities.Auth;

namespace Enigma.Server.Data.Repositories.Auth
{
    public class UsuarioRepository : GenericRepository<Usuario>
    {
        public UsuarioRepository(EnigmaDbContext context) : base(context)
        {
        }

        
    }
};