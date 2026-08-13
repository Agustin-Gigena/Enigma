using System.Text.RegularExpressions;
using Xunit;

namespace Enigma.Test.Architecture;

public class ArchitectureTests
{
  [Fact]
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

    Assert.True(violatingFiles.Count == 0,
        "Se encontraron archivos DTO fuera de Shared:\n" + string.Join("\n", violatingFiles));
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
    string fileName = Path.GetFileName(path);
    if (Regex.IsMatch(fileName, "dto", RegexOptions.IgnoreCase))
    {
      return true;
    }

    string content = File.ReadAllText(path);
    return Regex.IsMatch(content, @"\b[A-Za-z_][A-Za-z0-9_]*dto[A-Za-z0-9_]*\b", RegexOptions.IgnoreCase);
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
