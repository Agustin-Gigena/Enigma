namespace Enigma.Server.Data.Entities.Auth
{
    public class Usuario : GenericEntity
    {
        public string NombreUsuario { get; set; } = null!;
        public string CorreoElectronico { get; set; } = null!;
        public string Contrasena { get; set; } = null!;
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    }
}