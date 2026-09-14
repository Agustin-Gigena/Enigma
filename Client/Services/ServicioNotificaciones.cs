using MudBlazor;

namespace Enigma.Client.Services;

/// <summary>
/// Punto ÚNICO de notificación al usuario. Toda la app informa resultados por
/// acá (éxitos, errores de carga, errores de servidor, advertencias, info);
/// ninguna página arma su propio cartel ni usa estado--exito/estado--error.
/// El error opcional de HttpResponseMessage se resuelve vía HttpSeguro para
/// mostrar el detalle que manda la API.
/// </summary>
public sealed class ServicioNotificaciones(ISnackbar snackbar)
{
    private static Action<SnackbarOptions> Opciones => opciones =>
    {
        opciones.VisibleStateDuration = 4000;
        opciones.SnackbarVariant = Variant.Filled;
    };

    public void Exito(string mensaje) => snackbar.Add(mensaje, Severity.Success, Opciones);

    public void Info(string mensaje) => snackbar.Add(mensaje, Severity.Info, Opciones);

    public void Advertencia(string mensaje) => snackbar.Add(mensaje, Severity.Warning, Opciones);

    public void Error(string mensaje) => snackbar.Add(mensaje, Severity.Error, Opciones);

    /// <summary>Notifica un error de operación HTTP: muestra el detalle que
    /// expone la API si lo hay, o el mensaje pedido si no.</summary>
    public async Task ErrorAsync(string mensaje, HttpResponseMessage? respuesta)
    {
        string detalle = await HttpSeguro.LeerErrorAsync(respuesta);
        snackbar.Add(string.IsNullOrEmpty(detalle) ? mensaje : $"{mensaje} {detalle}",
            Severity.Error, Opciones);
    }

    /// <summary>Atajo para "respuesta nula o fallida": notification con el mensaje dado.</summary>
    public Task ErrorAsync(bool fallo, string mensaje, HttpResponseMessage? respuesta) =>
        fallo ? ErrorAsync(mensaje, respuesta) : Task.CompletedTask;
}
