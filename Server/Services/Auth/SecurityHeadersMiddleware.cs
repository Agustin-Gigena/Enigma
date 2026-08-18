namespace Enigma.Server.Services.Auth;

/// <summary>
/// Adds security headers to all responses: CSP, X-Frame-Options, X-Content-Type-Options,
/// Referrer-Policy, Permissions-Policy, X-XSS-Protection.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Disable legacy XSS filter (modern browsers don't need it)
        headers["X-XSS-Protection"] = "0";

        // Control referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict browser features
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Content Security Policy — Blazor WASM needs 'wasm-unsafe-eval' for scripts
        // and 'unsafe-inline' for scoped CSS
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'wasm-unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'";

        await _next(context);
    }
}

/// <summary>Extension method for registering the security headers middleware.</summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
