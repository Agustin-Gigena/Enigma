using System.Diagnostics;
using NUnit.Framework;

namespace Enigma.Test.E2E;

/// <summary>
/// Fixture compartido para tests E2E: levanta Server (:8081) y Client (:8080)
/// via dotnet run, y los mata al finalizar todos los tests.
/// Requiere MySQL disponible — si no, los tests fallan.
/// </summary>
[SetUpFixture]
public class E2EWebFixture
{
    public static int ServerPort { get; } = 8081;
    public static int ClientPort { get; } = 8080;
    public static string ServerUrl { get; } = $"http://localhost:{ServerPort}";
    public static string ClientUrl { get; } = $"http://localhost:{ClientPort}";

    private static Process? _serverProcess;
    private static Process? _clientProcess;

    private static string RepoRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Enigma.slnx")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new InvalidOperationException("No se encontró la raíz del repo.");
        }
    }

    [OneTimeSetUp]
    public async Task StartServers()
    {
        _serverProcess = StartDotnet("Server/Enigma.Server.csproj", ServerPort);
        _clientProcess = StartDotnet("Client/Enigma.Client.csproj", ClientPort);

        // El Server es una API sin ruta raíz: /health es el endpoint canónico
        // de "está arriba". El Client sí sirve / (Blazor WASM).
        await WaitForUrl($"{ServerUrl}/health", TimeSpan.FromSeconds(60));
        await WaitForUrl(ClientUrl, TimeSpan.FromSeconds(60));
    }

    [OneTimeTearDown]
    public void StopServers()
    {
        KillProcess(_serverProcess);
        KillProcess(_clientProcess);
    }

    private static Process StartDotnet(string project, int port)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {project} --no-build --urls http://localhost:{port}",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"No se pudo iniciar dotnet run para {project}");
    }

    private static async Task WaitForUrl(string url, TimeSpan timeout)
    {
        using HttpClient client = new();
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                // URL absoluta: si fuera relativa, HttpClient la combinaría con
                // BaseAddress y el path pedido no sería el esperado.
                HttpResponseMessage resp = await client.GetAsync(url);
                if (resp.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Servidor en {url} no respondió en {timeout.TotalSeconds}s");
    }

    private static void KillProcess(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch { }
    }
}
