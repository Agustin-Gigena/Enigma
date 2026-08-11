using Microsoft.JSInterop;

namespace Enigma.Client.Services;

/// <summary>
/// Toggle claro/oscuro: persiste "enigma_theme" en localStorage y aplica
/// data-theme en el elemento html. El tema inicial lo aplica un script inline
/// en index.html (antes del primer render, sin flash).
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _js;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string> ToggleAsync()
    {
        var actual = await ObtenerAsync();
        var nuevo = actual == "dark" ? "light" : "dark";
        await _js.InvokeVoidAsync("localStorage.setItem", "enigma_theme", nuevo);
        await _js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", nuevo);
        return nuevo;
    }

    public Task<string> ObtenerAsync()
    {
        return _js.InvokeAsync<string>("localStorage.getItem", "enigma_theme").AsTask();
    }
}
