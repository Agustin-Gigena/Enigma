namespace Enigma.Shared.Dtos;

/// <summary>Usuario autenticado, tal como se expone en la API y se persiste en el cliente.</summary>
public record UsuarioDto(int Id, string NombreUsuario, string? Correo);

/// <summary>Institución del usuario; <see cref="Tipo"/> es la enumeración como texto.</summary>
public record InstitucionDto(int Id, string Nombre, string Tipo);

/// <summary>Cuerpo HTTP de POST /auth/login (sin token — el JWT va en cookie HttpOnly).</summary>
public record LoginBody(UsuarioDto Usuario, List<InstitucionDto> Instituciones);

/// <summary>Resultado del login del lado del cliente (éxito + payload, o error mostrable).</summary>
public record LoginResult(bool Ok, string? Error = null, LoginBody? Datos = null);

/// <summary>Cuerpo de POST /auth/login.</summary>
public record LoginRequest(string Usuario, string Contrasena);

/// <summary>Sesión actual: usuario + institución activa + secciones visibles (espejo de las claims del JWT).</summary>
public record SesionDto(UsuarioDto Usuario, int? InstitucionActivaId, List<string> Permisos);
/// <summary>Rol del sistema (solo id y nombre para la UI de asignación).</summary>
public record RolDto(int Id, string Nombre);
/// <summary>Usuario miembro de la institución activa con sus roles en ella.</summary>
public record UsuarioInstitucionDto(UsuarioDto Usuario, List<string> Roles);
/// <summary>Cuerpo de POST /auth/institucion.</summary>
public record SeleccionInstitucionRequest(int InstitucionId);
/// <summary>Cuerpo de PUT administracion/usuarios/{id}/roles.</summary>
public record ActualizarRolesRequest(List<string> Roles);

/// <summary>Cuerpo de POST/PUT administracion/instituciones (crear y editar).</summary>
public record InstitucionRequest(string Nombre, string Tipo);

/// <summary>Cuerpo de POST administracion/usuarios (alta en la institución activa).</summary>
public record AltaUsuarioRequest(string NombreUsuario, string? Correo, string Contrasena, List<string> Roles);
