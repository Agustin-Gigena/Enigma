using Enigma.Shared.Modules;
using NUnit.Framework;

namespace Enigma.Test.Modules;

public class CatalogoModulosTest
{
    [Test]
    public void Secciones_ClavesYRutasUnicas()
    {
        Assert.That(CatalogoModulos.Secciones.Select(s => s.Clave).Distinct().Count(),
            Is.EqualTo(CatalogoModulos.Secciones.Count), "Claves de sección duplicadas.");
        Assert.That(CatalogoModulos.Secciones.Select(s => s.Ruta).Distinct().Count(),
            Is.EqualTo(CatalogoModulos.Secciones.Count), "Rutas duplicadas.");
    }

    [Test]
    public void Secciones_TodasReferencianModuloExistente()
    {
        List<string> modulos = [.. CatalogoModulos.Modulos.Select(m => m.Clave)];
        Assert.That(CatalogoModulos.Secciones,
            Has.All.Matches<SeccionDef>(s => modulos.Contains(s.ModuloClave, StringComparer.OrdinalIgnoreCase)));
    }

    [Test]
    public void ExisteSeccion_PorClave()
    {
        Assert.That(CatalogoModulos.ExisteSeccion("Administracion.Usuarios"), Is.True);
        Assert.That(CatalogoModulos.ExisteSeccion("Administracion.Inexistente"), Is.False);
        Assert.That(CatalogoModulos.ExisteSeccion(""), Is.False);
    }

    [Test]
    public void SeccionPorRuta_EncuentraYNulo()
    {
        SeccionDef? seccion = CatalogoModulos.SeccionPorRuta("/administracion/usuarios");
        Assert.That(seccion, Is.Not.Null);
        Assert.That(seccion!.Clave, Is.EqualTo("Administracion.Usuarios"));
        Assert.That(CatalogoModulos.SeccionPorRuta("/"), Is.Null);
        Assert.That(CatalogoModulos.SeccionPorRuta("/auth/login"), Is.Null);
    }
}
