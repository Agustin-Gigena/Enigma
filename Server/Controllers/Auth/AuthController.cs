using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;


namespace Enigma.Server.Controllers.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
  private readonly IUsuarioService _usuarioService;

  public AuthController(IUsuarioService usuarioService) => _usuarioService = usuarioService;


  /// <summary>
  /// Autentica con usuario + contraseña (ASP.NET Core Identity vía UsuarioService) y emite un JWT.
  /// Incluye las instituciones del usuario: el cliente las usa para la
  /// selección post-login sin roundtrip extra.
  /// </summary>
  [HttpPost("login")]
  public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
  {
    LoginResultado? resultado = await _usuarioService.LoginAsync(request.Usuario, request.Contrasena);
    if (resultado is null)
    {
      return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
    }

    (string? token, DateTime expiracion) = GenerarToken(resultado.Usuario);
    List<InstitucionDto> instituciones = resultado.Instituciones
        .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
        .ToList();

    return Ok(new LoginResponse(token, expiracion, ToDto(resultado.Usuario), instituciones));
  }

  /// <summary>Devuelve el usuario autenticado actual (valida el JWT de punta a punta).</summary>
  [Authorize]
  [HttpGet("me")]
  public ActionResult<UsuarioDto> Me()
  {
    Usuario? usuario = CurrentUserService.GetCurrentUser();
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
    Usuario? usuario = CurrentUserService.GetCurrentUser();
    if (usuario is null)
    {
      return Unauthorized(new { mensaje = "No autenticado." });
    }

    List<Institucion> instituciones = await _usuarioService.ObtenerInstitucionesAsync(usuario.Id);
    return Ok(instituciones
        .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
        .ToList());
  }

  private static UsuarioDto ToDto(Usuario usuario) => new(usuario.Id, usuario.UserName ?? "", usuario.Email);

  private static (string Token, DateTime Expiracion) GenerarToken(Usuario usuario)
  {
    string secret = Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET")
        ?? "enigma_dev_jwt_secret_cambiar_en_produccion";
    SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));
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
