using System.Security.Claims;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Enigma.Test.Auth;

public class CurrentUserServiceTest
{
    private static (HttpContextAccessor Accessor, DefaultHttpContext Context) ContextoConPrincipal(bool autenticado, string? userIdClaim = null)
    {
        DefaultHttpContext contexto = new();
        ClaimsIdentity identidad = autenticado
            ? new([new Claim(ClaimTypes.NameIdentifier, userIdClaim ?? "1")], "TestAuth")
            : new();
        contexto.User = new ClaimsPrincipal(identidad);
        return (new HttpContextAccessor { HttpContext = contexto }, contexto);
    }

    [SetUp]
    [TearDown]
    public void Limpiar() => CurrentUserService.EndScope();

    [Test]
    public void IsAuthenticated_SinScope_DevuelveFalse()
    {
        Assert.That(CurrentUserService.IsAuthenticated(), Is.False);
    }

    [Test]
    public void IsAuthenticated_ConPrincipalAutenticado_DevuelveTrue()
    {
        (HttpContextAccessor accessor, _) = ContextoConPrincipal(autenticado: true);
        CurrentUserService.BeginScope(accessor, _ => null);

        Assert.That(CurrentUserService.IsAuthenticated(), Is.True);
    }

    [Test]
    public void IsAuthenticated_ConPrincipalAnonimo_DevuelveFalse()
    {
        (HttpContextAccessor accessor, _) = ContextoConPrincipal(autenticado: false);
        CurrentUserService.BeginScope(accessor, _ => null);

        Assert.That(CurrentUserService.IsAuthenticated(), Is.False);
    }

    [Test]
    public void GetCurrentUser_ClaimNoParseable_DevuelveNull()
    {
        (HttpContextAccessor accessor, _) = ContextoConPrincipal(autenticado: true, userIdClaim: "abc");
        CurrentUserService.BeginScope(accessor, _ => new Usuario { Id = 1 });

        Assert.That(CurrentUserService.GetCurrentUser(), Is.Null);
    }

    [TestCase("true")]
    [TestCase("TRUE")]
    [TestCase("false")]
    [TestCase("basura")]
    [TestCase(null)]
    public void IsAuthRequired_OnlyLiteralTrueEsTrue(string? valor)
    {
        Environment.SetEnvironmentVariable("ENIGMA_AUTH_REQUIRED", valor);
        try
        {
            Assert.That(CurrentUserService.IsAuthRequired(), Is.EqualTo(string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENIGMA_AUTH_REQUIRED", null);
        }
    }

    [Test]
    public void GetCurrentUser_SinUsuarioYAuthRequired_LanzaConMensaje()
    {
        Environment.SetEnvironmentVariable("ENIGMA_AUTH_REQUIRED", "true");
        try
        {
            UnauthorizedAccessException? ex = Assert.Throws<UnauthorizedAccessException>(
                () => CurrentUserService.GetCurrentUser());
            Assert.That(ex!.Message, Is.EqualTo("El usuario no está autenticado."));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENIGMA_AUTH_REQUIRED", null);
        }
    }
}
