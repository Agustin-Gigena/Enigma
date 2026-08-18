using Enigma.Server.Options;
using Xunit;

namespace Enigma.Test.Options;

public class JwtOptionsTest
{
    // --- JwtSecretResolver.Resolve ---

    [Fact]
    public void Resolver_ConEnv_DevuelveElEnv()
    {
        Assert.Equal("un-secreto-bien-largo-y-aleatorio-1234567890",
            JwtSecretResolver.Resolve("un-secreto-bien-largo-y-aleatorio-1234567890", isDevelopment: false));
    }

    [Fact]
    public void Resolver_Dev_SinEnv_DevuelveFallback()
    {
        Assert.Equal("enigma_dev_jwt_secret_cambiar_en_produccion",
            JwtSecretResolver.Resolve(envSecret: null, isDevelopment: true));
    }

    [Fact]
    public void Resolver_Prod_SinEnv_Lanza()
    {
        Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: null, isDevelopment: false));
    }

    [Fact]
    public void Resolver_Prod_EnvVacio_Lanza()
    {
        Assert.Throws<InvalidOperationException>(
            () => JwtSecretResolver.Resolve(envSecret: "   ", isDevelopment: false));
    }

    // --- JwtOptions.EnsureValid ---

    [Fact]
    public void EnsureValid_SecretDeMasDe32Bytes_NoLanza()
    {
        JwtOptions opts = new() { Secret = new string('x', 40) };
        opts.EnsureValid();
    }

    [Theory]
    [InlineData("")]
    [InlineData("corto")]
    [InlineData(null)]
    public void EnsureValid_SecretInvalido_Lanza(string? secret)
    {
        JwtOptions opts = new() { Secret = secret! };
        Assert.Throws<InvalidOperationException>(opts.EnsureValid);
    }
}
