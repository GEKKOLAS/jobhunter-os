using JobHunter.Application.Identity;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Auth;

/// <summary>Issues and clears the session cookie (HttpOnly) and the CSRF cookie (readable by the web app).</summary>
public sealed class AuthCookies(IOptions<AuthOptions> authOptions)
{
    public void Issue(HttpResponse response, string sessionToken, string csrfToken)
    {
        var auth = authOptions.Value;
        response.Cookies.Append(auth.SessionCookieName, sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = auth.SecureCookies,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = auth.SessionAbsoluteLifetime,
        });
        response.Cookies.Append(auth.CsrfCookieName, csrfToken, new CookieOptions
        {
            HttpOnly = false, // double-submit: the web app reads it and echoes it in a header
            Secure = auth.SecureCookies,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = auth.SessionAbsoluteLifetime,
        });
    }

    public void Clear(HttpResponse response)
    {
        var auth = authOptions.Value;
        response.Cookies.Delete(auth.SessionCookieName, new CookieOptions { Path = "/" });
        response.Cookies.Delete(auth.CsrfCookieName, new CookieOptions { Path = "/" });
    }
}
