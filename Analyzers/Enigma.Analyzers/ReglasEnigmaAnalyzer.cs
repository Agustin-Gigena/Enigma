using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Enigma.Analyzers;

/// <summary>
/// Reglas de arquitectura y convenciones de Enigma (ex ArchitectureTests, ahora en el compilador).
/// Cada diagnóstico equivale 1:1 a un test del extinto Tests/Unit/Architecture/ArchitectureTests.cs;
/// validan lo mismo pero corren dentro de csc: costo marginal en cada build y feedback en el IDE.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class ReglasEnigmaAnalyzer : DiagnosticAnalyzer
{
    private const string CategoriaArquitectura = "Arquitectura";
    private const string CategoriaEstilo = "Estilo";
    private const string CategoriaNombres = "Nombres";
    private const string CategoriaLenguaje = "Lenguaje";

    // ─── Arquitectura ──────────────────────────────────────────────────────────

    private static readonly DiagnosticDescriptor EGX001 = new(
        "EGX001",
        "Los tipos Dto se definen solo en Shared",
        "El tipo '{0}' se define fuera de Shared. Regla: los DTOs viven únicamente en Enigma.Shared",
        CategoriaArquitectura, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX002 = new(
        "EGX002",
        "Solo los repositories acceden al contexto y a los DbSets",
        "Se referencia EnigmaDbContext/DbSet fuera de la capa de datos. Regla: solo los repositories acceden a la BD; los services consumen repositories",
        CategoriaArquitectura, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX003 = new(
        "EGX003",
        "Los repositories no devuelven ni mencionan DTOs",
        "El repository menciona 'Dto'. Regla: los repositories devuelven entidades; los services mapean a DTOs",
        CategoriaArquitectura, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX004 = new(
        "EGX004",
        "Un solo tipo por archivo",
        "El archivo declara clases: {0}, interfaces: {1}, records: {2}. Regla: 1 tipo por archivo y los records jamás se mezclan con clases o interfaces",
        CategoriaArquitectura, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX016 = new(
        "EGX016",
        "Páginas de sección registradas en el catálogo",
        "La ruta '{0}' {1}",
        CategoriaArquitectura, DiagnosticSeverity.Error, isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    // ─── Estilo ────────────────────────────────────────────────────────────────

    private static readonly DiagnosticDescriptor EGX005 = new(
        "EGX005",
        "Indentación con espacios, sin tabuladores",
        "El archivo contiene tabuladores. Regla de layout: indentar con 4 espacios",
        CategoriaEstilo, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX006 = new(
        "EGX006",
        "Llave de apertura en línea propia (Allman)",
        "La llave de apertura comparte línea con el código anterior. Regla de layout: la llave va en su propia línea",
        CategoriaEstilo, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX007 = new(
        "EGX007",
        "Usings fuera del namespace",
        "Directiva using dentro de un namespace con llaves. Regla: usar namespaces file-scoped",
        CategoriaEstilo, DiagnosticSeverity.Error, isEnabledByDefault: true);

    // ─── Nombres ───────────────────────────────────────────────────────────────

    private static readonly DiagnosticDescriptor EGX008 = new(
        "EGX008",
        "Interfaces con prefijo I",
        "La interfaz '{0}' no comienza con I",
        CategoriaNombres, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX009 = new(
        "EGX009",
        "Tipos en PascalCase",
        "El {0} '{1}' no está en PascalCase",
        CategoriaNombres, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX010 = new(
        "EGX010",
        "Métodos en PascalCase",
        "El método '{0}' no está en PascalCase",
        CategoriaNombres, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX011 = new(
        "EGX011",
        "Métodos async con sufijo Async",
        "El método async '{0}' no termina en Async",
        CategoriaNombres, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX012 = new(
        "EGX012",
        "Campos privados de instancia con prefijo _",
        "El campo privado '{0}' no usa prefijo _",
        CategoriaNombres, DiagnosticSeverity.Error, isEnabledByDefault: true);

    // ─── Lenguaje ──────────────────────────────────────────────────────────────

    private static readonly DiagnosticDescriptor EGX013 = new(
        "EGX013",
        "Interpolación en vez de string.Format",
        "Uso de string.Format. Regla: interpolación de cadenas ($\"...\")",
        CategoriaLenguaje, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX014 = new(
        "EGX014",
        "Sin this explícito",
        "Uso explícito de this. Regla: omitir el calificador salvo ambigüedad",
        CategoriaLenguaje, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EGX015 = new(
        "EGX015",
        "is / is not null en vez de ==/!= null",
        "Comparación de null con ==/!=. Regla: usar is / is not null",
        CategoriaLenguaje, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(EGX001, EGX002, EGX003, EGX004, EGX005, EGX006, EGX007,
            EGX008, EGX009, EGX010, EGX011, EGX012, EGX013, EGX014, EGX015, EGX016);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(inicio =>
        {
            string ensamblado = inicio.Compilation.Assembly.Name;
            bool esShared = ensamblado == "Enigma.Shared";
            bool esServer = ensamblado == "Enigma.Server";
            bool esClient = ensamblado == "Enigma.Client";
            bool esServerOCliente = esServer || esClient;
            bool esProduccion = esServerOCliente || esShared;

            // EGX001: el escaneo original cubría todo el repo, incluido el proyecto de tests.
            if (!esShared)
                inicio.RegisterSymbolAction(AnalizarTipoDto, SymbolKind.NamedType);

            inicio.RegisterSyntaxTreeAction(arbol =>
                AnalizarArbol(arbol, esServer, esServerOCliente, esProduccion));

            // EGX016: solo el cliente lleva el catálogo y las páginas razor como AdditionalFiles.
            if (esClient)
            {
                var catalogo = inicio.Options.AdditionalFiles.FirstOrDefault(archivo =>
                    archivo.Path.EndsWith("CatalogoModulos.cs", StringComparison.OrdinalIgnoreCase));
                var paginas = inicio.Options.AdditionalFiles
                    .Where(archivo => archivo.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                    .ToImmutableArray();
                if (catalogo is not null && paginas.Length > 0)
                    inicio.RegisterCompilationEndAction(fin =>
                        AnalizarRutas(fin, catalogo, paginas));
            }
        });
    }

    // EGX001: definiciones de tipos cuyo nombre contiene "Dto" fuera de Shared.
    private static void AnalizarTipoDto(SymbolAnalysisContext ctx)
    {
        var tipo = (INamedTypeSymbol)ctx.Symbol;
        if (!tipo.Name.Contains("Dto")) return;

        foreach (var ubicacion in tipo.Locations.Where(l => l.IsInSource))
        {
            if (EsSalidaCompilacion(RutaNormalizada(ubicacion.SourceTree!))) continue;
            ctx.ReportDiagnostic(Diagnostic.Create(EGX001, ubicacion, tipo.Name));
        }
    }

    // ─── Helpers compartidos ───────────────────────────────────────────────────

    private static string RutaNormalizada(SyntaxTree arbol) =>
        arbol.FilePath.Replace('\\', '/');

    // bin/obj: salida de compilación (fuera de alcance incluso para EGX001).
    private static bool EsSalidaCompilacion(string ruta) =>
        ruta.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
        || ruta.Contains("/obj/", StringComparison.OrdinalIgnoreCase);

    // Código generado + Migrations: fuera del alcance de las reglas de árbol,
    // igual que EsArchivoGenerado en el escaneo original.
    private static bool EsGenerado(string ruta) =>
        EsSalidaCompilacion(ruta)
        || ruta.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)
        || ruta.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || ruta.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);

    private static Location Ubicar(SyntaxTree arbol, TextSpan span) =>
        Location.Create(arbol.FilePath, span, arbol.GetLineSpan(span).Span);
}
