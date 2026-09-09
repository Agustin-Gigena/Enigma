using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;

namespace Enigma.Client.Services;

/// <summary>
/// Token de cancelación ligado a la navegación: en LocationChanging (ANTES de que el
/// router monte la página nueva) se cancela el token viejo — abortando los requests en
/// vuelo de la página que se abandona — y se emite uno nuevo, que es el que lee la
/// página que se monta a continuación.
///
/// OJO con el orden de eventos de Blazor WASM: el ciclo real es
/// LocationChanging → render de la página nueva → OnInitializedAsync → LocationChanged.
/// Rearmar en LocationChanged cancelaría el fetch de la página recién montada (que leyó
/// el token anterior); por eso el rearme vive en LocationChanging.
///
/// Centraliza el patrón que, repetido por componente (CTS + Dispose +
/// catch OperationCanceledException), no escala.
/// </summary>
public sealed class CancelacionNavegacion : IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly ILogger<CancelacionNavegacion> _logger;
    private readonly IDisposable _suscripcion;
    private CancellationTokenSource _cts = new();

    public CancelacionNavegacion(NavigationManager navigation, ILogger<CancelacionNavegacion> logger)
    {
        _navigation = navigation;
        _logger = logger;
        _suscripcion = _navigation.RegisterLocationChangingHandler(OnLocationChangingAsync);
    }

    /// <summary>Token vigente de la ruta actual; cambia al iniciar cada navegación.</summary>
    public CancellationToken Token => _cts.Token;

    private ValueTask OnLocationChangingAsync(LocationChangingContext contexto)
    {
        CancellationTokenSource viejo = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        _logger.LogDebug("Navegación hacia {Destino}: token anterior cancelado", contexto.TargetLocation);
        viejo.Cancel();
        viejo.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _suscripcion.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
