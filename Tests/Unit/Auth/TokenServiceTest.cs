using System.IdentityModel.Tokens.Jwt;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using Microsoft.Extensions.Options;
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
    Assert.That(expiracion, Is.GreaterThan(DateTime.UtcNow.AddHours(7)));
    Assert.That(expiracion, Is.LessThan(DateTime.UtcNow.AddHours(9)));
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
}
