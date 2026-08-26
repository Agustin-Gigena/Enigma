using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Enigma.Client.Services;

/// <summary>
/// Adjunta credentials: 'include' a cada pedido fetch. La página (:8080) y la API
/// (:8081) son orígenes distintos; con el modo por defecto ('same-origin') el
/// navegador ignora el Set-Cookie del login y nunca envía la cookie JWT en los
/// pedidos posteriores (todo /auth/me devolvía 401).
/// </summary>
public class CookieHandler : DelegatingHandler
{
    public CookieHandler() => InnerHandler = new HttpClientHandler();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
