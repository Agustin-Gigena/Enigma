using System.Reflection;
using Enigma.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using NUnit.Framework;

namespace Enigma.Test.Architecture
{
    public class SeccionControllerConventionTest
    {
        private readonly SeccionControllerConvention _sut = new();

        [Test]
        public void Apply_ControllerDeDominio_AgregaAuthorizeFilterConSeccion()
        {
            ControllerModel model = ModelFor(typeof(Enigma.Server.Controllers.Administracion.FakeUsuariosController), "Usuarios");
            _sut.Apply(model);
            AuthorizeFilter? filtro = model.Filters.OfType<AuthorizeFilter>().FirstOrDefault();
            Assert.That(filtro, Is.Not.Null, "Debe aplicar AuthorizeFilter automáticamente.");
        }

        [Test]
        public void Apply_SeccionFueraDeCatalogo_Lanza()
        {
            ControllerModel model = ModelFor(typeof(Enigma.Server.Controllers.Administracion.FakePersonasController), "Personas");
            Assert.That(() => _sut.Apply(model), Throws.InvalidOperationException);
        }

        [Test]
        public void Apply_ControllerDeAuth_QuedaExento()
        {
            ControllerModel model = ModelFor(typeof(Enigma.Server.Controllers.Auth.FakeAuthController), "Auth");
            _sut.Apply(model);
            Assert.That(model.Filters.OfType<AuthorizeFilter>().Any(), Is.False);
        }

        private static ControllerModel ModelFor(Type tipo, string nombre)
        {
            ControllerModel model = new(tipo.GetTypeInfo(), []);
            model.ControllerName = nombre;
            return model;
        }
    }
}

// Fakes en los namespaces que la convención inspecciona.
namespace Enigma.Server.Controllers.Administracion
{
    internal sealed class FakeUsuariosController : Microsoft.AspNetCore.Mvc.ControllerBase { }
    internal sealed class FakePersonasController : Microsoft.AspNetCore.Mvc.ControllerBase { }
}

namespace Enigma.Server.Controllers.Auth
{
    internal sealed class FakeAuthController : Microsoft.AspNetCore.Mvc.ControllerBase { }
}
