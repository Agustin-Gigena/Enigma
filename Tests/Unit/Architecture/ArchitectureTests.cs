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
