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
        Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: null, isDevelopment: false));
    }

    [Test]
    public void Resolver_Prod_EnvVacio_Lanza()
    {
        Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: "   ", isDevelopment: false));
    }

    // --- JwtOptions.EnsureValid ---

    [Test]
    public void EnsureValid_SecretDeMasDe32Bytes_NoLanza()
    {
        JwtOptions opts = new() { Secret = "b4Gx/tR4kDGKglumZlzfhTPTJ/+qVO3fHu4b2jvLgCc=" };
        opts.EnsureValid();
    }

    [TestCase("")]
    [TestCase("corto")]
    [TestCase(null)]
    public void EnsureValid_SecretInvalido_Lanza(string? secret)
    {
        JwtOptions opts = new() { Secret = secret! };
        Assert.Throws<InvalidOperationException>(opts.EnsureValid);
    }

    [Test]
    public void EnsureValid_SecretRepetido_Lanza()
    {
        JwtOptions opts = new() { Secret = new string('a', 40) };
        Assert.Throws<InvalidOperationException>(opts.EnsureValid);
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
