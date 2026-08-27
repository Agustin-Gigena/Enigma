using Enigma.Shared.Auth;
using Enigma.Shared.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Enigma.Server.Services.Auth;

/// <summary>
/// Regla de namespace: todo controller bajo Enigma.Server.Controllers.&lt;Dominio&gt; queda
/// protegido automáticamente con la sección <c>Dominio.ControllerName</c> del catálogo
/// (autenticado + tipo=sesion + role claim). Si la sección no existe en el catálogo,
/// el arranque falla (fail fast: imposible dejar un endpoint de dominio desprotegido).
/// Controllers/Auth (infraestructura de login) queda exento: se autorizan explícito.
/// </summary>
public sealed class SeccionControllerConvention : IControllerModelConvention
{
    private const string Prefijo = "Enigma.Server.Controllers.";

    /// <summary>Dominios de infraestructura sin sección de catálogo.</summary>
    private static readonly HashSet<string> DominiosExentos = ["Auth"];

    public void Apply(ControllerModel controller)
    {
        string ns = controller.ControllerType.Namespace ?? "";
        if (!ns.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            return;
        }
        string dominio = ns[Prefijo.Length..];
        if (dominio.Length == 0 || dominio.Contains('.') || DominiosExentos.Contains(dominio))
        {
            return;
        }

        string seccion = $"{dominio}.{controller.ControllerName}";
        if (!CatalogoModulos.ExisteSeccion(seccion))
        {
            throw new InvalidOperationException(
                $"El controller {controller.ControllerType.FullName} resuelve la sección '{seccion}' " +
                "que no existe en CatalogoModulos. Agregá la sección al catálogo o mové el controller.");
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(EnigmaClaims.Tipo, EnigmaClaims.Sesion)
            .RequireRole(seccion)
            .Build();
        controller.Filters.Add(new AuthorizeFilter(policy));
    }
}
