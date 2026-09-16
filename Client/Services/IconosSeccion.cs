using MudBlazor;

namespace Enigma.Client.Services;

/// <summary>Icono Material por sección del catálogo (para cards y menús).</summary>
public static class IconosSeccion
{
    public static string Para(string clave) => clave switch
    {
        var c when c.EndsWith("Usuarios") => Icons.Material.Filled.People,
        var c when c.EndsWith("Instituciones") => Icons.Material.Filled.AccountBalance,
        _ => Icons.Material.Filled.Apps
    };
}
