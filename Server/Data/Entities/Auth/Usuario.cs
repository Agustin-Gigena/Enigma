using Enigma.Server.Services.Interfaces;

namespace Enigma.Server.Data.Entities.Auth
{
    public class Usuario : GenericEntity
    {
        public Usuario(ICurrentUserService currentUserService) : base(currentUserService)
        {
            
        }
        public string NombreUsuario { get; set; } = null!;
        public string CorreoElectronico { get; set; } = null!;
        public string Contrasena { get; set; } = null!;
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    }
}