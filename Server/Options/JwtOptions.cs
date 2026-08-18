using System.Text;

namespace Enigma.Server.Options;

/// <summary>Configuración del secret JWT. <see cref="EnsureValid"/> aplica el fail-fast de longitud y entropía.</summary>
public sealed class JwtOptions
{
    public const int MinSecretBytes = 32;

    public string Secret { get; init; } = "";

    /// <summary>Lanza si el secret no alcanza el mínimo de bytes o tiene baja entropía. Se llama en startup.</summary>
    public void EnsureValid()
    {
        int bytes = string.IsNullOrEmpty(Secret) ? 0 : Encoding.UTF8.GetByteCount(Secret);
        if (bytes < MinSecretBytes)
        {
            throw new InvalidOperationException(
                $"ENIGMA_JWT_SECRET debe tener >= {MinSecretBytes} bytes (actual: {bytes}). La app no arranca.");
        }

        if (!HasMinimumEntropy(Secret))
        {
            throw new InvalidOperationException(
                "ENIGMA_JWT_SECRET tiene baja entropía (caracteres repetidos o patrones débiles). Usá un generador de secretos.");
        }
    }

    /// <summary>Verifica que el secret tenga entropía mínima: no todos los mismos caracteres, no patrones comunes débiles.</summary>
    public static bool HasMinimumEntropy(string secret, int minLength = 32)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < minLength)
            return false;

        // Reject if all characters are the same
        if (secret.Distinct().Count() == 1)
            return false;

        // Reject common weak patterns
        string[] weak = ["password", "secret", "123456", "admin", "changeme", "enigma_dev"];
        string lower = secret.ToLowerInvariant();
        if (weak.Any(w => lower.Contains(w)))
            return false;

        return true;
    }
}
