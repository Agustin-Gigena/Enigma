using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Enigma.Test.Architecture;

public class ArchitectureTests
{
    [Test]
    public void TodosLosDtosDebenEstarSoloEnShared()
    {
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string[] excludedDirectories = new[]
        {
            Path.Combine(repoRoot, ".git"),
            Path.Combine(repoRoot, "bin"),
            Path.Combine(repoRoot, "obj"),
            Path.Combine(repoRoot, ".vs"),
            Path.Combine(repoRoot, "Tests", "TestResults")
        };

        List<string> violatingFiles = Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInSharedFolder(path))
            .Where(path => !IsExcluded(path, excludedDirectories))
            .Where(IsDtoCandidate)
            .OrderBy(path => path)
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .ToList();

        Assert.That(violatingFiles.Count, Is.EqualTo(0),
            "Se encontraron archivos DTO fuera de Shared:\n" + string.Join("\n", violatingFiles));
    }

    [Test]
    public void SoloLosRepositoriesAccedenAlContextoYDbSets()
    {
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string serverRoot = Path.Combine(repoRoot, "Server");

        // Regla arquitectónica: los services NUNCA acceden a EnigmaDbContext ni DbSets;
        // todo acceso a datos vive en Repositories. Zonas permitidas: la capa de datos
        // (contexto, factoría de diseño, migraciones, repositorios) y el bootstrap del
        // host (Program.cs registra el DbContext y auto-migra en dev).
        List<string> permitidos =
        [
            Path.Combine(serverRoot, "Data", "Repositories"),
            Path.Combine(serverRoot, "Data", "EnigmaDbContext.cs"),
            Path.Combine(serverRoot, "Data", "EnigmaDbContextFactory.cs"),
            Path.Combine(serverRoot, "Migrations"),
            Path.Combine(serverRoot, "Program.cs"),
        ];

        Regex patronAccesoContext = new(@"EnigmaDbContext|DbSet\s*<");
        List<string> violadores = Directory.EnumerateFiles(serverRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !permitidos.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, p, StringComparison.OrdinalIgnoreCase)))
            .Where(path => patronAccesoContext.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .OrderBy(path => path)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Archivos fuera de la capa de datos que referencian EnigmaDbContext/DbSet. " +
            "Regla: solo los Repositories acceden a la BD; los services consumen repositories:\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void RepositoriesNoDevuelvenDtos()
    {
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string repositoriesRoot = Path.Combine(repoRoot, "Server", "Data", "Repositories");

        // Regla arquitectónica: los repositories devuelven ENTIDADES, nunca DTOs;
        // el mapeo entidad→DTO es responsabilidad de los services. El escaneo textual
        // cubre tanto firmas (Task<List<XxxDto>>) como usings de Enigma.Shared.Dtos.
        List<string> violadores = Directory.EnumerateFiles(repositoriesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("Dto", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .OrderBy(path => path)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Repositories que mencionan DTOs. Regla: los repositories devuelven entidades; " +
            "los services mapean a DTOs:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void UnSoloTipoPorArchivo()
    {
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string[] raices = [Path.Combine(repoRoot, "Server"), Path.Combine(repoRoot, "Client")];

        // Regla arquitectónica: 1 tipo por archivo — máx 1 clase, máx 1 interfaz,
        // máx 1 record, y los records JAMÁS mezclados con clases o interfaces
        // (un record de resultado vive en su propio archivo). Los tipos anidados
        // dentro de otro cuentan como parte del dueño del archivo, no por separado.
        // Alcance: código de producción (Server + Client); quedan fuera Migrations
        // (generado), bin/obj y Shared (AuthDtos.cs agrupa los DTOs a propósito).
        List<string> violadores = raices
            .SelectMany(raiz => Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
            .Where(path => !EsArchivoGenerado(path))
            .Select(path => (Ruta: path, Conteos: ContarTiposTopLevel(File.ReadAllText(path))))
            .Where(x => x.Conteos.Clases > 1
                     || x.Conteos.Interfaces > 1
                     || x.Conteos.Records > 1
                     || (x.Conteos.Records >= 1 && x.Conteos.Clases + x.Conteos.Interfaces >= 1))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)} " +
                         $"(clases: {x.Conteos.Clases}, interfaces: {x.Conteos.Interfaces}, records: {x.Conteos.Records})")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Archivos con más de un tipo (o records mezclados con clases/interfaces). " +
            "Regla: 1 tipo por archivo:\n" + string.Join("\n", violadores));
    }

    private static bool EsArchivoGenerado(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static (int Clases, int Interfaces, int Records) ContarTiposTopLevel(string codigo)
    {
        // Cuenta declaraciones class/interface/record en profundidad 0 de llaves
        // (los archivos usan namespace con punto y coma, así que los tipos de nivel
        // superior quedan a profundidad 0; lo anidado —métodos, tipos internos— a ≥1).
        // Antes: se descartan comentarios, strings y cláusulas `where X : class`
        // (la restricción genérica contiene la palabra "class" a profundidad 0).
        string limpio = QuitarComentariosYStrings(codigo);
        limpio = Regex.Replace(limpio, @"\bwhere\s+\w+\s*:\s*[^{;]*", "where");
        int clases = 0, interfaces = 0, records = 0, profundidad = 0;
        foreach (Match m in Regex.Matches(limpio, @"\b(?:class|interface|record)\b|\{|\}"))
        {
            switch (m.Value)
            {
                case "{": profundidad++; break;
                case "}": profundidad--; break;
                case "class" when profundidad == 0: clases++; break;
                case "interface" when profundidad == 0: interfaces++; break;
                case "record" when profundidad == 0: records++; break;
            }
        }
        return (clases, interfaces, records);
    }

    private static string QuitarComentariosYStrings(string codigo)
    {
        codigo = Regex.Replace(codigo, @"//[^\n]*", "");                       // comentario de línea
        codigo = Regex.Replace(codigo, @"/\*[\s\S]*?\*/", "");                 // comentario de bloque
        codigo = Regex.Replace(codigo, "@?\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");  // literales de cadena
        return codigo;
    }

    // ─── Convenciones de estilo de código (learn.microsoft.com/dotnet/csharp/
    //     fundamentals/coding-style/coding-conventions) ─────────────────────────
    // Chequeos textuales línea a línea sobre el código de producción
    // (Server + Client + Shared, sin bin/obj/Migrations), con comentarios y
    // literales de cadena eliminados para evitar falsos positivos.

    private static string[] RaicesDeProduccion(string repoRoot) =>
    [
        Path.Combine(repoRoot, "Server"),
        Path.Combine(repoRoot, "Client"),
        Path.Combine(repoRoot, "Shared"),
    ];

    private static IEnumerable<(string Ruta, string[] LineasLimpias)> FuentesDeProduccion(string repoRoot)
    {
        foreach (string raiz in RaicesDeProduccion(repoRoot))
        {
            foreach (string ruta in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
            {
                if (EsArchivoGenerado(ruta))
                {
                    continue;
                }
                yield return (ruta, QuitarComentariosYStrings(File.ReadAllText(ruta)).Split('\n'));
            }
        }
    }

    [Test]
    public void Layout_IndentacionConEspaciosSinTabulaciones()
    {
        // Convención de layout (MS): indentar con espacios, no tabuladores.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        List<string> violadores = RaicesDeProduccion(repoRoot)
            .SelectMany(raiz => Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
            .Where(path => !EsArchivoGenerado(path))
            .Where(path => File.ReadAllLines(path).Any(linea => linea.Contains('\t')))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .OrderBy(path => path)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Archivos con tabuladores. Regla de layout: indentar con 4 espacios:\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void Layout_LlavesEnLineaPropia()
    {
        // Convención de layout (MS): la llave de apertura va en su propia línea
        // (estilo Allman) para bloques de control y miembros. Se admite el cuerpo
        // de una sola línea cuando abre y cierra en la misma (ej. `public Rol() { }`).
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex control = new(@"^\s*(?:else if|if|for|foreach|while|switch|catch|lock|using)\b.*\)\s*\{\s*$");
        Regex miembro = new(@"^\s*(?:public|private|protected|internal)\b.*\)\s*\{\s*$");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Where(x => control.IsMatch(x.linea) || miembro.IsMatch(x.linea))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} {x.linea.Trim()}")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Bloques con llave de apertura en la misma línea. Regla de layout: la llave va en su propia línea:\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void Layout_UsingsFueraDelNamespace()
    {
        // Convención (MS): las directivas using van fuera del namespace. Con
        // namespaces file-scoped (`namespace X;`) es imposible violarlo; este test
        // custodia que no reaparezcan namespaces con llaves + usings adentro.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        List<string> violadores = FuentesDeProduccion(repoRoot)
            .Select(f => (f.Ruta, Codigo: string.Join('\n', f.LineasLimpias)))
            .Where(x => Regex.IsMatch(x.Codigo, @"namespace\s+[\w.]+\s*\{[^{}]*\busing\s"))
            .Select(x => Path.GetRelativePath(repoRoot, x.Ruta))
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Namespaces con llaves que contienen usings. Regla: usings fuera del namespace (file-scoped):\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void Nombres_InterfazConPrefijoI()
    {
        // Convención de nombres (MS): las interfaces comienzan con I mayúscula.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"\binterface\s+([A-Za-z_]\w*)");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => patron.Matches(string.Join('\n', f.LineasLimpias))
                .Cast<Match>().Select(m => (f.Ruta, Nombre: m.Groups[1].Value)))
            .Where(x => !x.Nombre.StartsWith('I'))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}: interfaz '{x.Nombre}' sin prefijo I")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Interfaces sin prefijo I:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void Nombres_TiposEnPascalCase()
    {
        // Convención de nombres (MS): clases, records y structs en PascalCase.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"\b(class|record|struct)\s+([A-Za-z_]\w*)");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => patron.Matches(string.Join('\n', f.LineasLimpias))
                .Cast<Match>().Select(m => (f.Ruta, Tipo: m.Groups[1].Value, Nombre: m.Groups[2].Value)))
            .Where(x => !char.IsUpper(x.Nombre[0]))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}: {x.Tipo} '{x.Nombre}' no está en PascalCase")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Tipos fuera de PascalCase:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void Nombres_MetodosEnPascalCase()
    {
        // Convención de nombres (MS): métodos (públicos y privados) en PascalCase.
        // Solo declaraciones con modificador explícito: las funciones locales van
        // sin él y quedan fuera del alcance de este chequeo textual.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"^\s*(?:public|private|protected|internal)\s+(?:(?:static|async|override|virtual|sealed|new|partial|extern)\s+)*[\w<>\[\],.? ]+?\s+([A-Za-z_]\w*)\s*\(");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Select(x => (x.Ruta, x.indice, Nombre: patron.Match(x.linea) is Match m && m.Success ? m.Groups[1].Value : null))
            .Where(x => x.Nombre is not null && !char.IsUpper(x.Nombre[0]))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} método '{x.Nombre}' no está en PascalCase")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Métodos fuera de PascalCase:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void Nombres_MetodosAsincronosConSufijoAsync()
    {
        // Convención de nombres (MS, sección async/await): los métodos async terminan
        // con el sufijo Async.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"^\s*(?:public|private|protected|internal)\s+(?:static\s+)?async\s+[\w<>\[\],.? ]+?\s+([A-Za-z_]\w*)\s*\(");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Select(x => (x.Ruta, x.indice, Nombre: patron.Match(x.linea) is Match m && m.Success ? m.Groups[1].Value : null))
            .Where(x => x.Nombre is not null && !x.Nombre.EndsWith("Async", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} método async '{x.Nombre}' sin sufijo Async")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Métodos async sin sufijo Async:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void Nombres_CamposPrivadosDeInstanciaConPrefijoGuionBajo()
    {
        // Convención de nombres (.NET): los campos de instancia privados usan
        // _camelCase. Los static readonly y const pueden ir en PascalCase
        // (convención adoptada por el proyecto), por eso quedan fuera del chequeo.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Where(x => x.linea.Contains("private", StringComparison.Ordinal)
                     && !x.linea.Contains(" static ", StringComparison.Ordinal)
                     && !x.linea.Contains("const ", StringComparison.Ordinal)
                     && !x.linea.Contains('(')
                     && !Regex.IsMatch(x.linea, @"\b(class|interface|record|struct|delegate|event)\b"))
            .Select(x => (x.Ruta, x.indice, Nombre: Regex.Match(x.linea, @"private\s+(?:(?:readonly|volatile)\s+)*(?:[\w<>\[\],.?]+\s+)?([A-Za-z_]\w*)\s*(?:=[^;]*)?;\s*$") is Match m && m.Success ? m.Groups[1].Value : null))
            .Where(x => x.Nombre is not null && !x.Nombre.StartsWith('_'))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} campo '{x.Nombre}' sin prefijo _")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Campos privados de instancia sin prefijo _:\n" + string.Join("\n", violadores));
    }

    [Test]
    public void Lenguaje_InterpolacionEnVezDeStringFormat()
    {
        // Convención de lenguaje (MS): para cadenas cortas, interpolación en lugar
        // de string.Format.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        List<string> violadores = FuentesDeProduccion(repoRoot)
            .Where(f => f.LineasLimpias.Any(l => l.Contains("string.Format(", StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(repoRoot, f.Ruta))
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Usos de string.Format. Regla: interpolación de cadenas ($\"...\"):\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void Lenguaje_SinThisExplicito()
    {
        // Convención de lenguaje (MS): evitar `this.` salvo que sea necesario para
        // desambiguar. El modificador de métodos de extensión (`this string s`) no
        // usa punto y queda fuera del patrón.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"\bthis\.");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Where(x => patron.IsMatch(x.linea))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} {x.linea.Trim()}")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Usos explícitos de this. Regla: omitir el calificador salvo ambigüedad:\n"
            + string.Join("\n", violadores));
    }

    [Test]
    public void Lenguaje_IsNotNullEnVezDeComparacionConNull()
    {
        // Convención de lenguaje (MS): usar los operadores is / is not para probar
        // null, en lugar de comparaciones de igualdad.
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        Regex patron = new(@"!=\s*null|==\s*null");

        List<string> violadores = FuentesDeProduccion(repoRoot)
            .SelectMany(f => f.LineasLimpias.Select((linea, indice) => (f.Ruta, linea, indice)))
            .Where(x => patron.IsMatch(x.linea))
            .Select(x => $"{Path.GetRelativePath(repoRoot, x.Ruta)}:{x.indice + 1} {x.linea.Trim()}")
            .OrderBy(x => x)
            .ToList();

        Assert.That(violadores, Is.Empty,
            "Comparaciones de null con ==/!=. Regla: usar is / is not null:\n"
            + string.Join("\n", violadores));
    }
    [Test]
    public void PaginasDeSeccion_EstanRegistradasEnCatalogo()
    {
        string repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string paginas = Path.Combine(repoRoot, "Client", "Pages");
        List<string> rutas = Directory.EnumerateFiles(paginas, "*.razor", SearchOption.AllDirectories)
            .SelectMany(RutasDePagina)
            .ToList();

        List<string> registradas = [.. Enigma.Shared.Modules.CatalogoModulos.Secciones.Select(s => s.Ruta)];

        List<string> sinRegistrar = rutas.Where(r => !registradas.Contains(r)).ToList();
        Assert.That(sinRegistrar, Is.Empty,
            "Hay páginas con @page bajo /administracion (u otro módulo) fuera del catálogo:\n"
            + string.Join("\n", sinRegistrar));

        List<string> sinPagina = registradas.Where(r => !rutas.Contains(r)).ToList();
        Assert.That(sinPagina, Is.Empty,
            "Secciones del catálogo sin página razor:\n" + string.Join("\n", sinPagina));
    }

    private static bool IsInSharedFolder(string path)
    {
        string normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string segment = Path.DirectorySeparatorChar + "Shared" + Path.DirectorySeparatorChar;
        return normalized.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExcluded(string path, string[] excludedDirectories)
    {
        return excludedDirectories.Any(excluded =>
            path.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, excluded, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDtoCandidate(string path)
    {
        string content = File.ReadAllText(path);
        // Matchea solo DEFINICIONES de tipos (class/record/struct) cuyo nombre contiene
        // "Dto" — no meras menciones. Así no marca imports (`using Enigma.Shared.Dtos;`)
        // ni usos de tipos DTO (InstitucionDto, etc.), que es lo esperado fuera de Shared,
        // ni el propio texto de este test.
        return Regex.IsMatch(content, @"(?:class|record|struct)\s+\w*Dto\w*", RegexOptions.IgnoreCase);
    }

    private static IEnumerable<string> RutasDePagina(string archivo)
    {
        // Solo rutas @page de primer nivel de módulo (ej. /administracion/...):
        // las páginas de auth/raíz no son secciones.
        foreach (string linea in File.ReadAllLines(archivo))
        {
            if (linea.TrimStart().StartsWith("@page", StringComparison.Ordinal))
            {
                string ruta = linea.Split('"')[1];
                if (ruta.Count(c => c == '/') >= 2 && !ruta.StartsWith("/auth/", StringComparison.Ordinal))
                {
                    yield return ruta;
                }
            }
        }
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? current = new(startDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Enigma.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("No se pudo localizar la raíz del repositorio (Enigma.slnx).");
    }
}
