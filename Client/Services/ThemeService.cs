using Microsoft.JSInterop;

namespace Enigma.Client.Services;

/// <summary>
/// Estado del modo claro/oscuro que alimenta a <c>MudThemeProvider</c>.
/// La preferencia vive en localStorage ("enigma_theme"); un script inline de
/// index.html resuelve y persiste el valor inicial antes del primer render
/// (sin flash, coincide con el splash oscuro de carga). Todo cambio de tema
/// también aplica <c>data-theme</c> en &lt;html&gt; y el meta theme-color,
/// para que el CSS propio (tokens --enigma-*) acompañe a la paleta MudBlazor.
/// </summary>
public sealed class ThemeService : IDisposable
{
    public const string ClaveStorage = "enigma_theme";

    private readonly IJSRuntime _js;
    private bool? _oscuro;

    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>Null hasta que la interop lee la preferencia guardada.</summary>
    public bool? Oscuro => _oscuro;

    /// <summary>Notifica cambios de modo (ida y vuelta con MudThemeProvider).</summary>
    public event Action? Cambiado;

    /// <summary>Lee la preferencia persistida una sola vez (idempotente) y
    /// aplica data-theme para el CSS propio de la app.</summary>
    public async Task InicializarAsync()
    {
        if (_oscuro is not null)
        {
            return;
        }

        string? guardado = await _js.InvokeAsync<string?>("localStorage.getItem", ClaveStorage);
        _oscuro = guardado != "light"; // dark por defecto: coincide con el splash y con prefers-color-scheme en dev.
        await AplicarAsync();
        Cambiado?.Invoke();
    }

    /// <summary>Fija el modo, persiste, aplica data-theme y notifica. Es la
    /// fuente de verdad: MudThemeProvider reporta acá sus toggles y la UI
    /// llama acá para cambiar.</summary>
    public void Fijar(bool oscuro)
    {
        if (_oscuro == oscuro)
        {
            return;
        }

        _oscuro = oscuro;
        _ = AplicarAsync();
        Cambiado?.Invoke();
    }

    /// <summary>Puente con el CSS propio: data-theme en &lt;html&gt; (tokens
    /// --enigma-*) + meta theme-color del navegador. MudBlazor pinta sus
    /// componentes por su lado; esto alinea el resto de la página.</summary>
    private async Task AplicarAsync()
    {
        string tema = _oscuro == true ? "dark" : "light";
        await _js.InvokeVoidAsync("eval",
            $"document.documentElement.setAttribute('data-theme','{tema}');" +
            "var m=document.querySelector('meta[name=theme-color]');" +
            $"if(m)m.setAttribute('content','{(tema == "dark" ? "#0C2524" : "#FFFFFF")}');");
        await _js.InvokeVoidAsync("localStorage.setItem", ClaveStorage, tema);
    }

    public void Dispose() => Cambiado = null;
}
