namespace Enigma.Shared.Auth;

/// <summary>Nombres de claims custom de Enigma (JWT y role claims de Identity).</summary>
public static class EnigmaClaims
{
    public const string Tipo = "tipo";
    public const string PreAutenticacion = "pre-autenticacion";
    public const string Sesion = "sesion";
    public const string Institucion = "institucion";
    /// <summary>Role claim de Identity sobre un Rol: habilita una sección del catálogo.</summary>
    public const string Seccion = "seccion";
}
