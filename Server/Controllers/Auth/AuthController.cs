using System.Threading.RateLimiting;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;


namespace Enigma.Server.Controllers.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly ITokenService _tokenService;

    public AuthController(IUsuarioService usuarioService, ITokenService tokenService)
    {
        _usuarioService = usuarioService;
        _tokenService = tokenService;
    }


    /// <summary>
    /// Autentica con usuario + contraseña (ASP.NET Core Identity vía UsuarioService) y emite un JWT.
    /// Incluye las instituciones del usuario: el cliente las usa para la
    /// selección post-login sin roundtrip extra.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginBody>> Login([FromBody] LoginRequest request)
    {
        LoginResultado? resultado = await _usuarioService.LoginAsync(request.Usuario, request.Contrasena);
        if (resultado is null)
        {
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
        }

        (string token, DateTime expiracion) = _tokenService.GenerarAccessToken(resultado.Usuario);
        List<InstitucionDto> instituciones = resultado.Instituciones
            .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
            .ToList();

        return Ok(new LoginBody(ToDto(resultado.Usuario), instituciones));
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
}
