using System.Net.Http.Headers;
using System.Text;

namespace ArkJsonValidator.Middleware;

public class BasicAuthMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            Challenge(ctx);
            return;
        }

        try
        {
            var parsed = AuthenticationHeaderValue.Parse(authHeader!);
            if (!parsed.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                Challenge(ctx); return;
            }
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.Parameter!)).Split(':', 2);
            var validUser = config["BasicAuth:Username"] ?? "admin";
            var validPass = config["BasicAuth:Password"] ?? "ark2024!";

            if (credentials.Length == 2 && credentials[0] == validUser && credentials[1] == validPass)
            {
                await next(ctx);
                return;
            }
        }
        catch { }

        Challenge(ctx);
    }

    private static void Challenge(HttpContext ctx)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"ARK JSON Validator API\"";
    }
}
