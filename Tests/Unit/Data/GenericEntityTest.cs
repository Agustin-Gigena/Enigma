using System.Security.Claims;
using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Services.Auth;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Enigma.Test.Data;

public class GenericEntityTest
{
    private static Institucion NuevaEntidad() => new() { Nombre = "Inst test", Tipo = TipoInstitucion.Curso };

    /// <summary>Siembra el scope ambiental con el usuario dado (mismo mecanismo que CurrentUserMiddleware).</summary>
    private static IDisposable AmbientScope(Usuario usuario)
    {
        DefaultHttpContext contexto = new();
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString())], "TestAuth"));
        HttpContextAccessor accessor = new() { HttpContext = contexto };
        CurrentUserService.BeginScope(accessor, _ => usuario);
        return new ScopeCleanup();
    }

    private sealed class ScopeCleanup : IDisposable
    {
        public void Dispose() => CurrentUserService.EndScope();
    }

    [SetUp]
    public void LimpiarScope() => CurrentUserService.EndScope();

    [Test]
    public void SetCreadoPor_ConUsuarioExplicito_NoRequiereAmbiental()
    {
        Usuario usuario = new() { Id = 1, UserName = "creador" };
        Institucion entidad = NuevaEntidad();

        entidad.SetCreadoPor(usuario);

        Assert.That(entidad.CreadoPor, Is.SameAs(usuario));
        Assert.That(entidad.CreadoEn, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-5)));
    }

    [Test]
    public void SetCreadoPor_SinUsuarioNiAmbiental_LanzaConMensaje()
    {
        Institucion entidad = NuevaEntidad();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => entidad.SetCreadoPor());
        Assert.That(ex!.Message, Is.EqualTo(
            "No se puede establecer el usuario creador porque no hay un usuario autenticado."));
    }

    [Test]
    public void SetCreadoPor_SinUsuario_ConAmbiental_UsaElAmbiental()
    {
        Usuario ambiental = new() { Id = 2, UserName = "ambiental" };
        Institucion entidad = NuevaEntidad();

        using (AmbientScope(ambiental))
        {
            entidad.SetCreadoPor();
        }

        Assert.That(entidad.CreadoPor, Is.SameAs(ambiental));
    }

    [Test]
    public void SetModificadoPor_SinUsuarioNiAmbiental_LanzaConMensaje()
    {
        Institucion entidad = NuevaEntidad();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => entidad.SetModificadoPor());
        Assert.That(ex!.Message, Is.EqualTo(
            "No se puede establecer el usuario modificador porque no hay un usuario autenticado."));
    }

    [Test]
    public void SetModificadoPor_ConAmbiental_SeteaUsuarioYFecha()
    {
        Usuario ambiental = new() { Id = 3, UserName = "modificador" };
        Institucion entidad = NuevaEntidad();

        using (AmbientScope(ambiental))
        {
            entidad.SetModificadoPor();
        }

        Assert.That(entidad.ModificadoPor, Is.SameAs(ambiental));
        Assert.That(entidad.ModificadoEn, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-5)));
    }

    [Test]
    public void SetBorradoLogico_True_ConAmbiental_SeteaBorradorYFecha()
    {
        Usuario ambiental = new() { Id = 4, UserName = "borrador" };
        Institucion entidad = NuevaEntidad();

        using (AmbientScope(ambiental))
        {
            entidad.SetBorradoLogico(true);
        }

        Assert.That(entidad.BorradoLogico, Is.True);
        Assert.That(entidad.BorradoPor, Is.SameAs(ambiental));
        Assert.That(entidad.BorradoEn, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-5)));
    }

    [Test]
    public void SetBorradoLogico_True_SinAmbiental_LanzaConMensaje()
    {
        Institucion entidad = NuevaEntidad();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => entidad.SetBorradoLogico(true));
        Assert.That(ex!.Message, Is.EqualTo(
            "No se puede establecer el usuario borrador porque no hay un usuario autenticado."));
    }

    [Test]
    public void SetBorradoLogico_False_ConAmbiental_LimpiaYRegistraModificacion()
    {
        Usuario ambiental = new() { Id = 5, UserName = "restaurador" };
        Institucion entidad = NuevaEntidad();
        using (AmbientScope(ambiental))
        {
            entidad.SetBorradoLogico(true);
        }

        using (AmbientScope(ambiental))
        {
            entidad.SetBorradoLogico(false);
        }

        Assert.That(entidad.BorradoLogico, Is.False);
        Assert.That(entidad.BorradoPor, Is.Null);
        Assert.That(entidad.BorradoEn, Is.Null);
        Assert.That(entidad.ModificadoPor, Is.SameAs(ambiental), "Restaurar registra quién modificó.");
        Assert.That(entidad.ModificadoEn, Is.Not.Null);
    }

    [Test]
    public void SetBorradoLogico_False_SinAmbiental_LanzaDeModificador()
    {
        Institucion entidad = NuevaEntidad();

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => entidad.SetBorradoLogico(false));

        Assert.That(ex!.Message, Is.EqualTo(
            "No se puede establecer el usuario modificador porque no hay un usuario autenticado."));
    }
}
