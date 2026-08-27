using System.Security.Claims;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Enigma.Shared.Auth;
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
    private readonly IMembresiaService _membresiaService;

    private const string CookieName = "enigma_token";
    private const int CookieMaxAgeSeconds = 8 * 60 * 60; // 8 hours
    private const int PreAuthCookieMaxAgeSeconds = 5 * 60; // 5 minutes

    public AuthController(
        IUsuarioService usuarioService,
        ITokenService tokenService,
        IMembresiaService membresiaService)
    {
        _usuarioService = usuarioService;
        _tokenService = tokenService;
        _membresiaService = membresiaService;
    }


    /// <summary>
    /// Autentica con usuario + contraseña (ASP.NET Core Identity vía UsuarioService) y emite
    /// el token de pre-autenticación (5 min) como cookie HttpOnly: solo sirve para elegir
    /// institución. El body trae usuario + instituciones, nunca el token.
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

        (string token, _) = _tokenService.GenerarTokenPreAutenticacion(resultado.Usuario);
        SetCookieToken(token, PreAuthCookieMaxAgeSeconds);

        List<InstitucionDto> instituciones = resultado.Instituciones
            .Select(i => new InstitucionDto(i.Id, i.Nombre, i.Tipo.ToString()))
            .ToList();
        return Ok(new LoginBody(ToDto(resultado.Usuario), instituciones));
    }

    /// <summary>Elimina la cookie de autenticación (logout).</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.None,
            Secure = true,
        });
        return NoContent();
    }

    private void SetCookieToken(string token, int maxAgeSeconds)
    {
        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(maxAgeSeconds),
        });
    }

    /// <summary>Espejo del JWT para el cliente (cookie HttpOnly): usuario, institución activa y permisos.</summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<SesionDto> Me()
    {
        Usuario? usuario = CurrentUserService.GetCurrentUser();
        if (usuario is null)
        {
            return Unauthorized(new { mensaje = "No autenticado." });
        }

        int? institucion = int.TryParse(User.FindFirst(EnigmaClaims.Institucion)?.Value, out int id) ? id : null;
        List<string> permisos = [.. User.FindAll(ClaimTypes.Role).Select(c => c.Value)];
        return Ok(new SesionDto(ToDto(usuario), institucion, permisos));
    }

    /// <summary>Instituciones a las que tiene acceso el usuario autenticado (pre-auth alcanza).</summary>
    [Authorize(Policy = "PreAutenticacion")]
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

    /// <summary>
    /// Elige la institución de trabajo: valida membresía activa y re-emite la cookie con el
    /// token de sesión (8 h) que lleva la institución y una claim role por sección visible.
    /// </summary>
    [HttpPost("institucion")]
    [Authorize(Policy = "PreAutenticacion")]
    public async Task<ActionResult<SesionDto>> SeleccionarInstitucion([FromBody] SeleccionInstitucionRequest request)
    {
        Usuario? usuario = CurrentUserService.GetCurrentUser();
        if (usuario is null)
        {
            return Unauthorized(new { mensaje = "No autenticado." });
        }

        Membresia? membresia = await _membresiaService.ObtenerMembresiaAsync(usuario.Id, request.InstitucionId);
        if (membresia is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "No tenés membresía activa en esa institución." });
        }

        List<string> secciones = await _membresiaService.ObtenerSeccionesAsync(usuario.Id, request.InstitucionId);
        (string token, _) = _tokenService.GenerarTokenSesion(usuario, request.InstitucionId, secciones);
        SetCookieToken(token, CookieMaxAgeSeconds);
        return Ok(new SesionDto(ToDto(usuario), request.InstitucionId, secciones));
    }

    // Stryker disable once String: UserName null es inalcanzable via Identity (UserName es requerido no-nulo)
    private static UsuarioDto ToDto(Usuario usuario) => new(usuario.Id, usuario.UserName ?? "", usuario.Email);
}
