using Enigma.Server.Data.Repositories.Auth;

namespace Enigma.Server.Services.Auth;

/// <summary>
/// Seeds the ambient CurrentUserService scope for the duration of the request
/// and always clears it afterwards (hygiene: no leaking entity/principal into
/// thread-pool threads).
/// </summary>
public class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserMiddleware(RequestDelegate next, IHttpContextAccessor accessor)
    {
        _next = next;
        _accessor = accessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Resolved from the request scope: the scoped DbContext stays valid for
        // the whole request, so the Lazy<Usuario?> inside the scope can resolve
        // at any point (even after the middleware's own frame continues).
        var repo = context.RequestServices.GetRequiredService<UsuarioRepository>();
        try
        {
            CurrentUserService.BeginScope(_accessor, id => repo.GetById(id));
            await _next(context);
        }
        finally
        {
            CurrentUserService.EndScope();
        }
    }
}
