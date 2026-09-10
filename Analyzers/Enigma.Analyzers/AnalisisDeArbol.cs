using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Enigma.Analyzers;

// Reglas por árbol de sintaxis. Mapea 1:1 los chequeos del extinto ArchitectureTests
// (EGX002–EGX015); el alcance por proyecto se decide en el registro de la compilación.
public partial class ReglasEnigmaAnalyzer
{
    private static void AnalizarArbol(
        SyntaxTreeAnalysisContext ctx, bool esServer, bool esServerOCliente, bool esProduccion)
    {
        var arbol = ctx.Tree;
        string ruta = RutaNormalizada(arbol);
        if (EsGenerado(ruta)) return;

        var raiz = arbol.GetRoot(ctx.CancellationToken);
        string texto = raiz.ToFullString();

        if (esServer)
        {
            ExigirContextoSoloEnRepositories(ctx, arbol, raiz, ruta);   // EGX002
            if (ruta.Contains("/Data/Repositories/", StringComparison.OrdinalIgnoreCase))
                ReportarMencionDeDto(ctx, arbol, texto);                // EGX003
        }

        if (esServerOCliente)
            ExigirUnTipoPorArchivo(ctx, arbol, raiz);                   // EGX004

        if (!esProduccion) return;

        RechazarTabuladores(ctx, arbol, texto);                         // EGX005
        ExigirLlavesEnLineaPropia(ctx, arbol, raiz);                    // EGX006
        ExigirUsingsFueraDelNamespace(ctx, raiz);                       // EGX007
        ExigirPrefijoIEnInterfaces(ctx, raiz);                          // EGX008
        ExigirPascalCaseEnTipos(ctx, raiz);                             // EGX009
        ExigirPascalCaseEnMetodos(ctx, raiz);                           // EGX010
        ExigirSufijoAsync(ctx, raiz, ruta);                             // EGX011
        ExigirPrefijoGuionBajoEnCampos(ctx, raiz);                      // EGX012
        RechazarStringFormat(ctx, raiz);                                // EGX013
        RechazarThisExplicito(ctx, raiz);                               // EGX014
        RechazarComparacionConNull(ctx, raiz);                          // EGX015
    }

    // EGX002: EnigmaDbContext/DbSet solo en la capa de datos, migraciones y bootstrap del host.
    private static void ExigirContextoSoloEnRepositories(
        SyntaxTreeAnalysisContext ctx, SyntaxTree arbol, SyntaxNode raiz, string ruta)
    {
        bool permitido =
            ruta.Contains("/Data/Repositories/", StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith("/EnigmaDbContext.cs", StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith("/EnigmaDbContextFactory.cs", StringComparison.OrdinalIgnoreCase)
            || ruta.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith("/Program.cs", StringComparison.OrdinalIgnoreCase);
        if (permitido) return;

        var nodo = raiz.DescendantNodes().OfType<SimpleNameSyntax>().FirstOrDefault(nombre =>
            nombre is IdentifierNameSyntax id && id.Identifier.ValueText == "EnigmaDbContext"
            || nombre is GenericNameSyntax gen && gen.Identifier.ValueText == "DbSet");
        if (nodo is not null)
            ctx.ReportDiagnostic(Diagnostic.Create(EGX002, nodo.GetLocation()));
    }

    // EGX003: ninguna mención de "Dto" en archivos de repositories.
    private static void ReportarMencionDeDto(SyntaxTreeAnalysisContext ctx, SyntaxTree arbol, string texto)
    {
        int indice = texto.IndexOf("Dto", StringComparison.Ordinal);
        if (indice >= 0)
            ctx.ReportDiagnostic(Diagnostic.Create(EGX003, Ubicar(arbol, new TextSpan(indice, 3))));
    }

    // EGX004: 1 tipo por archivo (los anidados cuentan como parte del dueño).
    private static void ExigirUnTipoPorArchivo(SyntaxTreeAnalysisContext ctx, SyntaxTree arbol, SyntaxNode raiz)
    {
        var toplevel = raiz.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(tipo => tipo.Parent is not TypeDeclarationSyntax)
            .ToList();
        if (toplevel.Count == 0) return;

        int clases = toplevel.Count(tipo => tipo is ClassDeclarationSyntax);
        int interfaces = toplevel.Count(tipo => tipo is InterfaceDeclarationSyntax);
        int records = toplevel.Count(tipo => tipo is RecordDeclarationSyntax);

        bool viola = clases > 1
            || interfaces > 1
            || records > 1
            || (records >= 1 && clases + interfaces >= 1);
        if (!viola) return;

        ctx.ReportDiagnostic(Diagnostic.Create(
            EGX004, Ubicar(arbol, toplevel[0].Keyword.Span), clases, interfaces, records));
    }

    // EGX005: ni un tabulador en el código de producción.
    private static void RechazarTabuladores(SyntaxTreeAnalysisContext ctx, SyntaxTree arbol, string texto)
    {
        int indice = texto.IndexOf('\t');
        if (indice >= 0)
            ctx.ReportDiagnostic(Diagnostic.Create(EGX005, Ubicar(arbol, new TextSpan(indice, 1))));
    }

    // EGX006: la llave de apertura de bloques de control y miembros va en línea propia
    // (Allman). Se admite el cuerpo de una sola línea que abre y cierra en la misma.
    private static void ExigirLlavesEnLineaPropia(SyntaxTreeAnalysisContext ctx, SyntaxTree arbol, SyntaxNode raiz)
    {
        foreach (var bloque in raiz.DescendantNodes().OfType<BlockSyntax>())
        {
            // Lambdas y funciones locales quedan fuera del alcance (convención adoptada).
            if (bloque.Parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) continue;

            var llave = bloque.OpenBraceToken;
            if (llave.IsMissing) continue;
            var anterior = llave.GetPreviousToken();
            if (anterior.IsKind(SyntaxKind.None)) continue;

            int lineaLlave = arbol.GetLineSpan(llave.Span).StartLinePosition.Line;
            if (arbol.GetLineSpan(anterior.Span).EndLinePosition.Line != lineaLlave) continue;

            // Cuerpo de una sola línea: admitido.
            if (!bloque.CloseBraceToken.IsMissing
                && arbol.GetLineSpan(bloque.CloseBraceToken.Span).EndLinePosition.Line == lineaLlave)
                continue;

            ctx.ReportDiagnostic(Diagnostic.Create(EGX006, llave.GetLocation()));
        }
    }

    // EGX007: usings fuera del namespace (custodia el uso de namespaces file-scoped).
    private static void ExigirUsingsFueraDelNamespace(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var directiva in raiz.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (directiva.Parent is NamespaceDeclarationSyntax)
                ctx.ReportDiagnostic(Diagnostic.Create(EGX007, directiva.GetLocation()));
    }

    // EGX008: interfaces con prefijo I mayúscula.
    private static void ExigirPrefijoIEnInterfaces(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var interfaz in raiz.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            string nombre = interfaz.Identifier.ValueText;
            if (nombre.Length == 0 || nombre[0] != 'I')
                ctx.ReportDiagnostic(Diagnostic.Create(EGX008, interfaz.Identifier.GetLocation(), nombre));
        }
    }

    // EGX009: clases, records y structs en PascalCase (cualquier profundidad).
    private static void ExigirPascalCaseEnTipos(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var tipo in raiz.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (tipo is InterfaceDeclarationSyntax) continue;
            string nombre = tipo.Identifier.ValueText;
            if (nombre.Length > 0 && !char.IsUpper(nombre[0]))
                ctx.ReportDiagnostic(Diagnostic.Create(
                    EGX009, tipo.Identifier.GetLocation(), tipo.Keyword.ValueText, nombre));
        }
    }

    // EGX010: métodos con modificador de accesibilidad en PascalCase. Las funciones
    // locales (sin modificador) y la implementación explícita de interfaces quedan fuera.
    private static void ExigirPascalCaseEnMetodos(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var metodo in raiz.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!TieneAccesibilidad(metodo.Modifiers)) continue;
            if (metodo.ExplicitInterfaceSpecifier is not null) continue;
            string nombre = metodo.Identifier.ValueText;
            if (nombre.Length > 0 && !char.IsUpper(nombre[0]))
                ctx.ReportDiagnostic(Diagnostic.Create(EGX010, metodo.Identifier.GetLocation(), nombre));
        }
    }

    // EGX011: métodos async terminan en Async. EXCEPCIÓN: acciones de controladores MVC —
    // el nombre define la ruta y ASP.NET Core no usa el sufijo ahí.
    private static void ExigirSufijoAsync(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz, string ruta)
    {
        if (ruta.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase)) return;

        foreach (var metodo in raiz.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!TieneAccesibilidad(metodo.Modifiers)) continue;
            if (!metodo.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))) continue;
            string nombre = metodo.Identifier.ValueText;
            if (!nombre.EndsWith("Async", StringComparison.Ordinal))
                ctx.ReportDiagnostic(Diagnostic.Create(EGX011, metodo.Identifier.GetLocation(), nombre));
        }
    }

    // EGX012: campos de instancia privados en _camelCase. Los static readonly y const
    // pueden ir en PascalCase (convención adoptada); los event van en otro nodo sintáctico.
    private static void ExigirPrefijoGuionBajoEnCampos(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var campo in raiz.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!campo.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) continue;
            if (campo.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword))) continue;

            foreach (var variable in campo.Declaration.Variables)
            {
                string nombre = variable.Identifier.ValueText;
                if (nombre.Length == 0 || nombre[0] != '_')
                    ctx.ReportDiagnostic(Diagnostic.Create(EGX012, variable.Identifier.GetLocation(), nombre));
            }
        }
    }

    // EGX013: string.Format → interpolación. `string` parsea como tipo predefinido.
    private static void RechazarStringFormat(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var acceso in raiz.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            if (acceso.Name.Identifier.ValueText == "Format"
                && acceso.Expression is PredefinedTypeSyntax predefinido
                && predefinido.Keyword.IsKind(SyntaxKind.StringKeyword))
                ctx.ReportDiagnostic(Diagnostic.Create(EGX013, acceso.GetLocation()));
    }

    // EGX014: omitir this. salvo desambiguación (los métodos de extensión no pasan por aquí).
    private static void RechazarThisExplicito(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var acceso in raiz.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            if (acceso.Expression is ThisExpressionSyntax)
                ctx.ReportDiagnostic(Diagnostic.Create(EGX014, acceso.Expression.GetLocation()));
    }

    // EGX015: probar null con is / is not, no con ==/!=.
    private static void RechazarComparacionConNull(SyntaxTreeAnalysisContext ctx, SyntaxNode raiz)
    {
        foreach (var binaria in raiz.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (!binaria.IsKind(SyntaxKind.EqualsExpression)
                && !binaria.IsKind(SyntaxKind.NotEqualsExpression))
                continue;
            if (EsLiteralNull(binaria.Left) || EsLiteralNull(binaria.Right))
                ctx.ReportDiagnostic(Diagnostic.Create(EGX015, binaria.GetLocation()));
        }
    }

    private static bool TieneAccesibilidad(SyntaxTokenList modificadores) =>
        modificadores.Any(m => m.IsKind(SyntaxKind.PublicKeyword)
            || m.IsKind(SyntaxKind.PrivateKeyword)
            || m.IsKind(SyntaxKind.ProtectedKeyword)
            || m.IsKind(SyntaxKind.InternalKeyword));


    private static bool EsLiteralNull(SyntaxNode? nodo) =>
        nodo is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression);
}
