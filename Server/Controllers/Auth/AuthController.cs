using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Enigma.Shared.Dtos;


namespace Enigma.Server.Controllers.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public AuthController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }


    /// <summary>
    /// Autentica con usuario + contraseña (ASP.NET Core Identity vía UsuarioService) y emite un JWT.
    /// Incluye las instituciones del usuario: el cliente las usa para la
    /// selección post-login sin roundtrip extra.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var resultado = await _usuarioService.LoginAsync(request.Usuario, request.Contrasena);
        if (resultado is null)
        {
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
        }

        var (token, expiracion) = GenerarToken(resultado.Usuario);
        var instituciones = resultado.Instituciones
            .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
            .ToList();

        return Ok(new LoginResponse(token, expiracion, ToDto(resultado.Usuario), instituciones));
    }

    /// <summary>Devuelve el usuario autenticado actual (valida el JWT de punta a punta).</summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<UsuarioDto> Me()
    {
        var usuario = CurrentUserService.GetCurrentUser();
        if (usuario is null)
        {
            return Unauthorized(new { mensaje = "No autenticado." });
        }
        return Ok(ToDto(usuario));
    }

    /// <summary>Instituciones a las que tiene acceso el usuario autenticado.</summary>
    [Authorize]
    [HttpGet("instituciones")]
    public async Task<ActionResult<List<InstitucionDto>>> Instituciones()
    {
        var usuario = CurrentUserService.GetCurrentUser();
        if (usuario is null)
        {
            return Unauthorized(new { mensaje = "No autenticado." });
        }

        var instituciones = await _usuarioService.ObtenerInstitucionesAsync(usuario.Id);
        return Ok(instituciones
            .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
            .ToList());
    }

    private static UsuarioDto ToDto(Usuario usuario) => new(usuario.Id, usuario.UserName ?? "", usuario.Email);

    private static (string Token, DateTime Expiracion) GenerarToken(Usuario usuario)
    {
        var secret = Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET")
            ?? "enigma_dev_jwt_secret_cambiar_en_produccion";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiracion = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "Enigma",
            audience: "Enigma.Client",
            claims: claims,
            expires: expiracion,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiracion);
    }
}
