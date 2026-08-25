using Enigma.Server.Options;
using NUnit.Framework;

namespace Enigma.Test.Options;

public class JwtOptionsTest
{
    // --- JwtSecretResolver.Resolve ---

    [Test]
    public void Resolver_ConEnv_DevuelveElEnv()
    {
        Assert.That(JwtSecretResolver.Resolve("un-secreto-bien-largo-y-aleatorio-1234567890", isDevelopment: false),
            Is.EqualTo("un-secreto-bien-largo-y-aleatorio-1234567890"));
    }

    [Test]
    public void Resolver_Dev_SinEnv_GeneraSecretoAleatorio()
    {
        string secret = JwtSecretResolver.Resolve(envSecret: null, isDevelopment: true);
        Assert.That(secret, Is.Not.Null.And.Not.Empty);
        Assert.That(secret.Length, Is.GreaterThanOrEqualTo(32),
            "El secreto generado debe tener al menos 32 caracteres.");
    }

    [Test]
    public void Resolver_Prod_SinEnv_Lanza()
    {
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: null, isDevelopment: false));
        Assert.That(ex!.Message, Is.EqualTo("ENIGMA_JWT_SECRET es obligatorio en Production (>= 32 bytes)."));
    }

    [Test]
    public void Resolver_Prod_EnvVacio_Lanza()
    {
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: "   ", isDevelopment: false));
        Assert.That(ex!.Message, Is.EqualTo("ENIGMA_JWT_SECRET es obligatorio en Production (>= 32 bytes)."));
    }

    // --- JwtOptions.EnsureValid ---

    [Test]
    public void EnsureValid_SecretDeMasDe32Bytes_NoLanza()
    {
        JwtOptions opts = new() { Secret = "b4Gx/tR4kDGKglumZlzfhTPTJ/+qVO3fHu4b2jvLgCc=" };
        opts.EnsureValid();
    }

    [TestCase("", 0)]
    [TestCase("corto", 5)]
    [TestCase(null, 0)]
    public void EnsureValid_SecretInvalido_Lanza(string? secret, int bytesEsperados)
    {
        JwtOptions opts = new() { Secret = secret! };
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(opts.EnsureValid);
        Assert.That(ex!.Message, Is.EqualTo(
            $"ENIGMA_JWT_SECRET debe tener >= 32 bytes (actual: {bytesEsperados}). La app no arranca."));
    }

    [Test]
    public void EnsureValid_SecretRepetido_Lanza()
    {
        JwtOptions opts = new() { Secret = new string('a', 40) };
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(opts.EnsureValid);
        Assert.That(ex!.Message, Is.EqualTo(
            "ENIGMA_JWT_SECRET tiene baja entropía (caracteres repetidos o patrones débiles). Usá un generador de secretos."));
    }

    // --- Entropy validation ---

    [Test]
    public void HasMinimumEntropy_SecretValido_DevuelveTrue()
    {
        Assert.That(JwtOptions.HasMinimumEntropy("b4Gx/tR4kDGKglumZlzfhTPTJ/+qVO3fHu4b2jvLgCc="), Is.True);
    }

    [Test]
    public void HasMinimumEntropy_TodosLosMismosCaracteres_DevuelveFalse()
    {
        Assert.That(JwtOptions.HasMinimumEntropy(new string('a', 40)), Is.False);
    }

    [Test]
    public void HasMinimumEntropy_PatronDebil_DevuelveFalse()
    {
        Assert.That(JwtOptions.HasMinimumEntropy("password_password_password_password"), Is.False);
    }

    [Test]
    public void HasMinimumEntropy_MuyCorto_DevuelveFalse()
    {
        Assert.That(JwtOptions.HasMinimumEntropy("corto"), Is.False);
    }
}
