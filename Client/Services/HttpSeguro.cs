using System.Net.Http.Json;

namespace Enigma.Client.Services;

/// <summary>
/// Envoltorios de HttpClient para los fetch de componentes: cancelación
/// (navegación/teardown) y fallo de red devuelven <c>null</c> en silencio en
/// lugar de lanzar — el componente desmontado ya no re-renderiza, y el que
/// sigue montado decide qué hacer con el <c>null</c>. Junto con
/// <see cref="CancelacionNavegacion"/> y el ErrorBoundary del layout,
/// componen el manejo central de errores/cancelación del cliente.
/// </summary>
public static class HttpSeguro
{
    /// <summary>GET deserializado; null si se canceló o falló la red.</summary>
    public static async Task<T?> GetSeguroAsync<T>(this HttpClient http, string url, CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(url, ct);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (HttpRequestException)
        {
            return default;
        }
    }

    /// <summary>POST JSON; null si se canceló o falló la red (el request puede no haber llegado).</summary>
    public static async Task<HttpResponseMessage?> PostSeguroAsync<TValor>(
        this HttpClient http, string url, TValor valor, CancellationToken ct = default)
    {
        try
        {
            return await http.PostAsJsonAsync(url, valor, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>PUT JSON; null si se canceló o falló la red (el request puede no haber llegado).</summary>
    public static async Task<HttpResponseMessage?> PutSeguroAsync<TValor>(
        this HttpClient http, string url, TValor valor, CancellationToken ct = default)
    {
        try
        {
            return await http.PutAsJsonAsync(url, valor, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
