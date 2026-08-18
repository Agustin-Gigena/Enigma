using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Enigma.Server.Services.Auth;

public interface ITokenService
{
  /// <summary>Genera el access JWT (issuer Enigma, audience Enigma.Client, TTL 8 h).</summary>
  (string Token, DateTime Expiracion) GenerarAccessToken(Usuario usuario);
}

public sealed class TokenService : ITokenService
{
  private readonly JwtOptions _jwt;

  public TokenService(IOptions<JwtOptions> jwt) => _jwt = jwt.Value;

  public (string Token, DateTime Expiracion) GenerarAccessToken(Usuario usuario)
  {
    SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwt.Secret));
    SigningCredentials credenciales = new(key, SecurityAlgorithms.HmacSha256);
    DateTime expiracion = DateTime.UtcNow.AddHours(8);

    List<Claim> claims = new()
    {
      new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
      new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
      new(ClaimTypes.Name, usuario.UserName ?? ""),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };

    JwtSecurityToken token = new(
        issuer: "Enigma",
        audience: "Enigma.Client",
        claims: claims,
        expires: expiracion,
        signingCredentials: credenciales);

    return (new JwtSecurityTokenHandler().WriteToken(token), expiracion);
  }
}
