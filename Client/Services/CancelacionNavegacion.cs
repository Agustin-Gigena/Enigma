using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Enigma.Client.Services;

/// <summary>
/// Token de cancelación ligado a la navegación: se cancela al cambiar de ruta
/// (los requests en vuelo de la página que se abandona se abortan) y se rearma
/// para la siguiente. Centraliza el patrón que, repetido por componente
/// (CTS + Dispose + catch OperationCanceledException), no escala.
/// </summary>
public sealed class CancelacionNavegacion : IDisposable
{
    private readonly NavigationManager _navigation;
    private CancellationTokenSource _cts = new();

    public CancelacionNavegacion(NavigationManager navigation)
    {
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>Token vigente de la ruta actual; cambia en cada navegación.</summary>
    public CancellationToken Token => _cts.Token;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        CancellationTokenSource viejo = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        viejo.Cancel();
        viejo.Dispose();
    }

    public void Dispose()
    {
        _navigation.LocationChanged -= OnLocationChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
