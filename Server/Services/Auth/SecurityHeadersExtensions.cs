using Microsoft.AspNetCore.Builder;

namespace Enigma.Server.Services.Auth;

/// <summary>Extension method for registering the security headers middleware.</summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder) => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
