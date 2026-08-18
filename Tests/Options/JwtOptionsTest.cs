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
    public void Resolver_Dev_SinEnv_DevuelveFallback()
    {
        Assert.That(JwtSecretResolver.Resolve(envSecret: null, isDevelopment: true),
            Is.EqualTo("enigma_dev_jwt_secret_cambiar_en_produccion"));
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
        JwtOptions opts = new() { Secret = new string('x', 40) };
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
}
