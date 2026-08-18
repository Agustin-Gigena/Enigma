using System.Text;

namespace Enigma.Server.Options;

/// <summary>Configuración del secret JWT. <see cref="EnsureValid"/> aplica el fail-fast de longitud.</summary>
public sealed class JwtOptions
{
    public const int MinSecretBytes = 32;

    public string Secret { get; init; } = "";

    /// <summary>Lanza si el secret no alcanza el mínimo de bytes. Se llama en startup.</summary>
    public void EnsureValid()
    {
        int bytes = string.IsNullOrEmpty(Secret) ? 0 : Encoding.UTF8.GetByteCount(Secret);
        if (bytes < MinSecretBytes)
        {
            throw new InvalidOperationException(
                $"ENIGMA_JWT_SECRET debe tener >= {MinSecretBytes} bytes (actual: {bytes}). La app no arranca.");
        }
    }
}
