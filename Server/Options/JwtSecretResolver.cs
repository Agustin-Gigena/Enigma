using System.Security.Cryptography;

namespace Enigma.Server.Options;

/// <summary>Resuelve el secret JWT según entorno: variable de entorno > generación aleatoria en dev > throw en prod.</summary>
public static class JwtSecretResolver
{
    public static string Resolve(string? envSecret, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(envSecret))
        {
            return envSecret;
        }

        if (isDevelopment)
        {
            byte[] random = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(random);
            string devSecret = Convert.ToBase64String(random);
            Console.WriteLine("[SECURITY] No ENIGMA_JWT_SECRET set — generated random dev secret. Set the env var for stable tokens across restarts.");
            return devSecret;
        }

        throw new InvalidOperationException(
            "ENIGMA_JWT_SECRET es obligatorio en Production (>= 32 bytes).");
    }
}
