using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Enigma.Analyzers;

// EGX016: toda página de sección (ruta con 2+ niveles, fuera de /auth/) registrada en el
// catálogo, y toda sección del catálogo con su página razor. El catálogo y las páginas
// llegan como AdditionalFiles del proyecto Client; las rutas se leen como texto, igual
// que hacía el test original.
public partial class ReglasEnigmaAnalyzer
{
    private static readonly Regex RegexLiteralDeTexto = new("\"([^\"]*)\"", RegexOptions.Compiled);

    private static void AnalizarRutas(
        CompilationAnalysisContext ctx, AdditionalText catalogo, ImmutableArray<AdditionalText> paginas)
    {
        var ct = ctx.CancellationToken;

        // Rutas registradas: literales "/..." en el fuente del catálogo (equivalente a
        // CatalogoModulos.Secciones; los módulos no llevan rutas).
        string textoCatalogo = catalogo.GetText(ct)!.ToString();
        SourceText fuenteCatalogo = SourceText.From(textoCatalogo);
        var registradas = new Dictionary<string, Location>();
        foreach (Match literal in RegexLiteralDeTexto.Matches(textoCatalogo))
        {
            string valor = literal.Groups[1].Value;
            if (!EsRutaDeSeccion(valor)) continue;
            var span = new TextSpan(literal.Groups[1].Index, literal.Groups[1].Length);
            if (!registradas.ContainsKey(valor))
                registradas[valor] = Location.Create(
                    catalogo.Path, span, fuenteCatalogo.Lines.GetLinePositionSpan(span));
        }

        // Rutas @page de Client/Pages.
        var usadas = new HashSet<string>();
        foreach (var pagina in paginas)
        {
            string texto = pagina.GetText(ct)!.ToString();
            SourceText fuente = SourceText.From(texto);
            int offset = 0;
            foreach (string linea in texto.Split('\n'))
            {
                if (linea.TrimStart().StartsWith("@page", StringComparison.Ordinal))
                {
                    string ruta = linea.Split('"')[1];
                    if (EsRutaDeSeccion(ruta) && !ruta.StartsWith("/auth/", StringComparison.Ordinal))
                    {
                        usadas.Add(ruta);
                        if (!registradas.ContainsKey(ruta))
                        {
                            Match literal = RegexLiteralDeTexto.Match(linea);
                            var span = new TextSpan(offset + literal.Groups[1].Index, literal.Groups[1].Length);
                            ctx.ReportDiagnostic(Diagnostic.Create(EGX016,
                                Location.Create(pagina.Path, span, fuente.Lines.GetLinePositionSpan(span)),
                                ruta, $"de la página '{Path.GetFileName(pagina.Path)}' no está registrada en el catálogo"));
                        }
                    }
                }
                offset += linea.Length + 1;
            }
        }

        foreach (var registrada in registradas)
        {
            if (usadas.Contains(registrada.Key)) continue;
            ctx.ReportDiagnostic(Diagnostic.Create(EGX016,
                registrada.Value, registrada.Key, "del catálogo no tiene página razor en Client/Pages"));
        }
    }

    private static bool EsRutaDeSeccion(string ruta) =>
        ruta.Length > 0 && ruta[0] == '/' && ruta.Count(caracter => caracter == '/') >= 2;
}
