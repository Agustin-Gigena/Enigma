using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using NUnit.Framework;

namespace Enigma.Test.Auth;

public class TokenServiceTest
{
    private static TokenService NewSut(string secret) =>
        new(Microsoft.Extensions.Options.Options.Create(new JwtOptions { Secret = secret }));

    [Test]
    public void GenerarAccessToken_DevuelveJwtDeTresPartes()
    {
        TokenService sut = NewSut(new string('k', 40));
        Usuario usuario = new() { Id = 7, UserName = "admin" };

        (string token, DateTime expiracion) = sut.GenerarAccessToken(usuario);

        Assert.That(token.Split('.').Length, Is.EqualTo(3));
        Assert.That(expiracion, Is.GreaterThan(DateTime.UtcNow.AddHours(7.9)), "TTL debe ser 8h exactos.");
        Assert.That(expiracion, Is.LessThan(DateTime.UtcNow.AddHours(8.1)), "TTL debe ser 8h exactos.");
    }

    [Test]
    public void GenerarAccessToken_EmiteIssuerYAudienceCorrectos()
    {
        TokenService sut = NewSut(new string('k', 40));
        Usuario usuario = new() { Id = 1, UserName = "admin" };

        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerarAccessToken(usuario).Token);

        Assert.That(decoded.Issuer, Is.EqualTo("Enigma"));
        Assert.That(decoded.Audiences, Does.Contain("Enigma.Client"));
        Assert.That(decoded.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value, Is.EqualTo("1"));
    }

    [Test]
    public void GenerarAccessToken_UserNameNull_EmiteClaimVacio()
    {
        TokenService sut = NewSut(new string('k', 40));
        Usuario usuario = new() { Id = 3, UserName = null! };

        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerarAccessToken(usuario).Token);

        Assert.That(decoded.Claims.First(c => c.Type == ClaimTypes.Name).Value, Is.Empty,
            "UserName null debe serializarse como claim vacío, no romper el token.");
    }

    [Test]
    public void GenerarTokenPreAutenticacion_TTL5MinYClaimTipo()
    {
        TokenService sut = NewSut(new string('k', 40));
        Usuario usuario = new() { Id = 7, UserName = "admin" };

        (string token, DateTime expiracion) = sut.GenerarTokenPreAutenticacion(usuario);

        Assert.That(expiracion, Is.GreaterThan(DateTime.UtcNow.AddMinutes(4.9)));
        Assert.That(expiracion, Is.LessThan(DateTime.UtcNow.AddMinutes(5.1)));
        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.That(decoded.Claims.First(c => c.Type == "tipo").Value, Is.EqualTo("pre-autenticacion"));
        Assert.That(decoded.Claims.Any(c => c.Type == "role"), Is.False, "El pre-auth no lleva roles.");
    }

    [Test]
    public void GenerarTokenSesion_TTL8hConInstitucionYRoles()
    {
        TokenService sut = NewSut(new string('k', 40));
        Usuario usuario = new() { Id = 7, UserName = "admin" };

        (string token, DateTime expiracion) = sut.GenerarTokenSesion(
            usuario, institucionId: 3, secciones: ["Administracion.Usuarios", "Administracion.Instituciones"]);

        Assert.That(expiracion, Is.GreaterThan(DateTime.UtcNow.AddHours(7.9)));
        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.That(decoded.Claims.First(c => c.Type == "tipo").Value, Is.EqualTo("sesion"));
        Assert.That(decoded.Claims.First(c => c.Type == "institucion").Value, Is.EqualTo("3"));
        // ClaimTypes.Role se serializa como "role" (outbound claim type map).
        List<string> roles = decoded.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.That(roles, Is.EquivalentTo(new[] { "Administracion.Usuarios", "Administracion.Instituciones" }));
    }
}
