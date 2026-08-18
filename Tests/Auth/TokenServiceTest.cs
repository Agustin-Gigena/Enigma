using System.IdentityModel.Tokens.Jwt;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Enigma.Test.Auth;

public class TokenServiceTest
{
  private static TokenService NewSut(string secret) =>
      new(Microsoft.Extensions.Options.Options.Create(new JwtOptions { Secret = secret }));

  [Fact]
  public void GenerarAccessToken_DevuelveJwtDeTresPartes()
  {
    TokenService sut = NewSut(new string('k', 40));
    Usuario usuario = new() { Id = 7, UserName = "admin" };

    (string token, DateTime expiracion) = sut.GenerarAccessToken(usuario);

    Assert.Equal(3, token.Split('.').Length);
    Assert.True(expiracion > DateTime.UtcNow.AddHours(7));
    Assert.True(expiracion < DateTime.UtcNow.AddHours(9));
  }

  [Fact]
  public void GenerarAccessToken_EmiteIssuerYAudienceCorrectos()
  {
    TokenService sut = NewSut(new string('k', 40));
    Usuario usuario = new() { Id = 1, UserName = "admin" };

    JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerarAccessToken(usuario).Token);

    Assert.Equal("Enigma", decoded.Issuer);
    Assert.Contains("Enigma.Client", decoded.Audiences);
    Assert.Equal("1", decoded.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
  }
}
