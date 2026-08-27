using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Enigma.Server.Services.Auth;

public interface ITokenService
{
    /// <summary>Token pre-autenticación (TTL 5 min): solo sirve para elegir institución.</summary>
    (string Token, DateTime Expiracion) GenerarTokenPreAutenticacion(Usuario usuario);

    /// <summary>Token de sesión (TTL 8 h): institución activa + una claim role por sección visible.</summary>
    (string Token, DateTime Expiracion) GenerarTokenSesion(Usuario usuario, int institucionId, IReadOnlyCollection<string> secciones);
}

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;

    public TokenService(IOptions<JwtOptions> jwt) => _jwt = jwt.Value;

    public (string Token, DateTime Expiracion) GenerarTokenPreAutenticacion(Usuario usuario)
        => Generar(usuario, TimeSpan.FromMinutes(5), claimsExtra: null, institucionId: null, secciones: null);

    public (string Token, DateTime Expiracion) GenerarTokenSesion(Usuario usuario, int institucionId, IReadOnlyCollection<string> secciones)
        => Generar(usuario, TimeSpan.FromHours(8), claimsExtra: null, institucionId, secciones);

    private (string Token, DateTime Expiracion) Generar(
        Usuario usuario, TimeSpan ttl, IEnumerable<Claim>? claimsExtra, int? institucionId, IReadOnlyCollection<string>? secciones)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwt.Secret));
        SigningCredentials credenciales = new(key, SecurityAlgorithms.HmacSha256);
        DateTime expiracion = DateTime.UtcNow.Add(ttl);

        string tipo = secciones is null ? EnigmaClaims.PreAutenticacion : EnigmaClaims.Sesion;
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(EnigmaClaims.Tipo, tipo),
        ];
        if (institucionId is not null)
        {
            claims.Add(new(EnigmaClaims.Institucion, institucionId.Value.ToString()));
        }
        if (secciones is not null)
        {
            // IdentityModel 8.x no acorta ClaimTypes.Role al serializar; se emite el nombre
            // corto "role" (convención JWT/OIDC, mapeado a ClaimTypes.Role al validar).
            claims.AddRange(secciones.Select(s => new Claim("role", s)));
        }
        if (claimsExtra is not null)
        {
            claims.AddRange(claimsExtra);
        }

        JwtSecurityToken token = new(
            issuer: "Enigma",
            audience: "Enigma.Client",
            claims: claims,
            expires: expiracion,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiracion);
    }
}
