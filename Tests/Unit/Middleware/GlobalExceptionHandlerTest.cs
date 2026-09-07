using System.Text.Json;
using Enigma.Server.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Enigma.Test.Middleware;

/// <summary>
/// Contrato del GlobalExceptionHandler: la cancelación (request abortado o token
/// interno) se traga sin respuesta de error; cualquier otra excepción responde
/// 500 JSON con mensaje genérico (sin detalles internos).
/// </summary>
public class GlobalExceptionHandlerTest
{
    private readonly GlobalExceptionHandler _sut = new(NullLogger<GlobalExceptionHandler>.Instance);

    private static HttpContext NuevoContexto()
    {
        HttpContext contexto = new DefaultHttpContext();
        contexto.Request.Method = "GET";
        contexto.Request.Path = "/administracion/usuarios";
        return contexto;
    }

    [Test]
    public async Task OperationCanceled_SeTraga_SinRespuestaDeError()
    {
        HttpContext contexto = NuevoContexto();

        bool manejada = await _sut.TryHandleAsync(
            contexto, new OperationCanceledException(), CancellationToken.None);

        Assert.That(manejada, Is.True, "La cancelación queda manejada por el handler global.");
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK),
            "Sin status de error: el cliente que abortó ya no escucha.");
    }

    [Test]
    public async Task ExcepcionGenerica_Devuelve500JsonGenerico()
    {
        HttpContext contexto = NuevoContexto();
        contexto.Response.Body = new MemoryStream();

        bool manejada = await _sut.TryHandleAsync(
            contexto, new InvalidOperationException("detalle interno sensible"), CancellationToken.None);

        Assert.That(manejada, Is.True);
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(contexto.Response.ContentType, Does.Contain("application/json"));

        contexto.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.That(body.RootElement.GetProperty("mensaje").GetString(),
            Is.EqualTo("Ocurrió un error inesperado. Intentá de nuevo más tarde."));
        string crudo = System.Text.Encoding.UTF8.GetString(((MemoryStream)contexto.Response.Body).ToArray());
        Assert.That(crudo, Does.Not.Contain("detalle interno sensible"),
            "El 500 no filtra detalles de la excepción.");
    }

    [Test]
    public async Task ExcepcionGenerica_ConRequestCancelado_IgualIntentaResponder()
    {
        // El 500 se escribe con CancellationToken.None: ni la cancelación del request
        // impide el intento de respuesta.
        HttpContext contexto = NuevoContexto();
        contexto.Response.Body = new MemoryStream();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        bool manejada = await _sut.TryHandleAsync(
            contexto, new InvalidOperationException(), cts.Token);

        Assert.That(manejada, Is.True);
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }
}
