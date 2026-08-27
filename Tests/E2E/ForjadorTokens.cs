using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Enigma.Test.E2E;

/// <summary>Forja tokens firmados con el secret fijo del E2EWebFixture.</summary>
public static class ForjadorTokens
{
    public static string Sesion(int usuarioId, int? institucionId, IEnumerable<string> roles)
    {
        JwtSecurityTokenHandler handler = new();
        List<Claim> claims =
        [
            new("sub", usuarioId.ToString()),
            new("nameid", usuarioId.ToString()),
            new("name", "forjado"),
            new("jti", Guid.NewGuid().ToString()),
            new("tipo", "sesion"),
        ];
        if (institucionId is not null)
        {
            claims.Add(new("institucion", institucionId.Value.ToString()));
        }
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        return handler.WriteToken(new JwtSecurityToken(
            issuer: "Enigma", audience: "Enigma.Client", claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(E2EWebFixture.JwtSecret)),
                SecurityAlgorithms.HmacSha256)));
    }
}
