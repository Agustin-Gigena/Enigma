namespace Enigma.Shared.Modules;

/// <summary>Módulo del menú (agrupador de secciones, ej. Administración).</summary>
public sealed record ModuloDef(string Clave, string Etiqueta, int Orden);

/// <summary>Sección navegable; <see cref="Clave"/> ("Modulo.Seccion") ES la clave de permiso.</summary>
public sealed record SeccionDef(string Clave, string ModuloClave, string Etiqueta, string Ruta, int Orden);

/// <summary>
/// Catálogo explícito de módulos y secciones: fuente única del menú (cliente) y de la
/// autorización por convención de namespace (server). Regla: toda página de sección se
/// registra acá; el guard del cliente la protege por su ruta.
/// </summary>
public static class CatalogoModulos
{
    public static readonly IReadOnlyList<ModuloDef> Modulos =
    [
        new("Administracion", "Administración", 1),
    ];

    public static readonly IReadOnlyList<SeccionDef> Secciones =
    [
        new("Administracion.Usuarios",      "Administracion", "Usuarios",      "/administracion/usuarios",      1),
        new("Administracion.Instituciones", "Administracion", "Instituciones", "/administracion/instituciones", 2),
    ];

    public static bool ExisteSeccion(string clave) =>
        Secciones.Any(s => s.Clave == clave);

    public static SeccionDef? SeccionPorRuta(string ruta) =>
        Secciones.FirstOrDefault(s => s.Ruta == ruta);
}
