using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Auth;
using Microsoft.AspNetCore.Identity;

namespace Enigma.Server.Services.Auth;

/// <summary>
/// Resultado del login con el usuario autenticado y sus instituciones.
/// </summary>
public record LoginResultado(Usuario Usuario, List<Institucion> Instituciones);

public interface IUsuarioService
{
  /// <summary>
  /// Autentica credenciales vía Identity (SignInManager + UserManager, con lockout).
  /// Devuelve null si las credenciales son inválidas o el usuario está borrado lógicamente.
  /// </summary>
  public Task<LoginResultado?> LoginAsync(string nombreUsuario, string contrasena);

  public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario);

  public Task<List<Institucion>> ObtenerInstitucionesAsync(int usuarioId);
}

public class UsuarioService : IUsuarioService
{
  private readonly SignInManager<Usuario> _signInManager;
  private readonly UserManager<Usuario> _userManager;
  private readonly UsuarioRepository _usuarioRepository;

  public UsuarioService(
      SignInManager<Usuario> signInManager,
      UserManager<Usuario> userManager,
      UsuarioRepository usuarioRepository)
  {
    _signInManager = signInManager;
    _userManager = userManager;
    _usuarioRepository = usuarioRepository;
  }

  public async Task<LoginResultado?> LoginAsync(string nombreUsuario, string contrasena)
  {
    SignInResult resultado = await _signInManager.PasswordSignInAsync(
        nombreUsuario, contrasena, isPersistent: false, lockoutOnFailure: true);
    if (!resultado.Succeeded)
    {
      return null;
    }

    Usuario? usuario = await _userManager.FindByNameAsync(nombreUsuario);
    if (usuario is null || usuario.BorradoLogico)
    {
      return null;
    }

    await _usuarioRepository.ActualizarLastLoginAsync(usuario.Id, DateTime.UtcNow);
    List<Institucion> instituciones = await _usuarioRepository.ObtenerInstitucionesAsync(usuario.Id);
    return new LoginResultado(usuario, instituciones);
  }

  public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario)
      => _userManager.FindByNameAsync(nombreUsuario);

  public Task<List<Institucion>> ObtenerInstitucionesAsync(int usuarioId)
      => _usuarioRepository.ObtenerInstitucionesAsync(usuarioId);
}
