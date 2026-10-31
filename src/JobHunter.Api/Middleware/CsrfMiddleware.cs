using JobHunter.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Middleware;

/// <summary>
/// Double-submit CSRF check (spec 02 §5): an unsafe request that carries a session cookie
/// must echo the CSRF cookie in the header. Requests without a session have nothing to
/// forge and are covered by rate limits instead.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next, IOptions<AuthOptions> authOptions)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task InvokeAsync(HttpContext context)
    {
        var auth = authOptions.Value;
        if (SafeMethods.Contains(context.Request.Method) || !context.Request.Cookies.ContainsKey(auth.SessionCookieName))
        {
            await next(context);
            return;
        }

        var cookie = context.Request.Cookies[auth.CsrfCookieName];
        var header = context.Request.Headers[auth.CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(cookie) || !string.Equals(cookie, header, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "CSRF token missing or invalid.",
                    Extensions = { ["errorCode"] = "csrf.invalid" },
                },
                options: null,
                contentType: "application/problem+json");
            return;
        }

        await next(context);
    }
}
