namespace Enigma.Client.Shared.Dialogos;

/// <summary>Datos que devuelve el diálogo de alta de usuario.</summary>
public sealed record DatosAltaUsuario(
    string Usuario, string? Correo, string Contrasena, List<string> Roles);
