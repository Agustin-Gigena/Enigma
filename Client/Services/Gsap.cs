using Microsoft.JSInterop;

namespace Enigma.Client.Services;

/// <summary>
/// Interfaz C# para las animaciones de la app (GSAP). Único punto de entrada:
/// las vistas inyectan este servicio y NUNCA llaman al JS (wwwroot/js/gsap.js)
/// por su cuenta. El adaptador aplica la curva de la casa (power3.out ≈
/// cubic-bezier(0.16, 1, 0.3, 1)) y respeta prefers-reduced-motion.
/// </summary>
public sealed class Gsap(IJSRuntime js)
{
    /// <summary>Entrada de vista: fade + slide del contenido tras navegar.</summary>
    public ValueTask VistaEntradaAsync(string selector) =>
        js.InvokeVoidAsync("EnigmaGsap.vistaEntrada", selector);

    /// <summary>Entrada escalonada de elementos (filas de tabla, tarjetas).</summary>
    public ValueTask EntradaEscalonadaAsync(string selector, float escalonado = 0.06f) =>
        js.InvokeVoidAsync("EnigmaGsap.entradaEscalonada", selector, escalonado);

    /// <summary>Resaltar un elemento como feedback de acción (guardado, selección).</summary>
    public ValueTask PulsoAsync(string selector) =>
        js.InvokeVoidAsync("EnigmaGsap.pulso", selector);

    /// <summary>Apertura de diálogo (overlay + tarjeta).</summary>
    public ValueTask DialogoAbrirAsync(string overlay, string tarjeta) =>
        js.InvokeVoidAsync("EnigmaGsap.dialogoAbrir", overlay, tarjeta);

    /// <summary>Cierre de diálogo; completa cuando la animación terminó.</summary>
    public Task DialogoCerrarAsync(string overlay, string tarjeta) =>
        js.InvokeAsync<object>("EnigmaGsap.dialogoCerrar", overlay, tarjeta).AsTask();
}
