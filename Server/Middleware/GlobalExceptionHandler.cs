using Microsoft.AspNetCore.Diagnostics;

namespace Enigma.Server.Middleware;

/// <summary>
/// Manejo GLOBAL de excepciones (IExceptionHandler): un solo lugar para toda la API.
///
/// - <see cref="OperationCanceledException"/>: el request se abortó (cliente navegó o
///   desconectó — ASP.NET Core cancela el token del request y EF lo propaga). NO es un
///   error: se registra en Debug y se traga; si la conexión ya no existe, escribir una
///   respuesta solo lanzaría otra excepción.
/// - Cualquier otra excepción: se registra con ruta y responde 500 JSON con un mensaje
///   genérico (sin filtrar stack traces ni detalles internos).
///
/// Registrado en Program.cs (AddExceptionHandler + UseExceptionHandler). Los endpoints
/// NO necesitan try/catch para esto.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            // Request abortado por el cliente (navegación/desconexión) o cancelación
            // interna del pipeline: nada que reportar — el cliente ya no escucha.
            logger.LogDebug(exception, "Request cancelado: {Metodo} {Ruta}",
                httpContext.Request.Method, httpContext.Request.Path);
            return true; // manejada: el middleware no busca otro handler ni re-lanza.
        }

        logger.LogError(exception, "Excepción no manejada: {Metodo} {Ruta}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        // CancellationToken.None a propósito: si el request original se canceló a mitad
        // de manejar otra excepción, igualmente intentamos responder.
        await httpContext.Response.WriteAsJsonAsync(
            new { mensaje = "Ocurrió un error inesperado. Intentá de nuevo más tarde." },
            CancellationToken.None);
        return true;
    }
}
