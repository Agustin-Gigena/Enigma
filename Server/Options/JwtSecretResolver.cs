namespace Enigma.Server.Options;

/// <summary>Resuelve el secret JWT según entorno: variable de entorno > fallback de dev > throw en prod.</summary>
public static class JwtSecretResolver
{
  public const string DevFallback = "enigma_dev_jwt_secret_cambiar_en_produccion";

  public static string Resolve(string? envSecret, bool isDevelopment)
  {
    if (!string.IsNullOrWhiteSpace(envSecret))
    {
      return envSecret;
    }

    return isDevelopment
        ? DevFallback
        : throw new InvalidOperationException(
            "ENIGMA_JWT_SECRET es obligatorio en Production (>= 32 bytes).");
  }
}
