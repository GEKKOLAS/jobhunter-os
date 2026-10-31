using System.Security.Claims;
using System.Text.Encodings.Web;
using JobHunter.Application.Common;
using JobHunter.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Auth;

public static class SessionAuthentication
{
    public const string Scheme = "Session";
    public const string SessionIdClaim = "jh:session_id";
}

/// <summary>
/// Authenticates a request from its session cookie: the cookie holds the raw token and
/// the database its SHA-256 (spec 02 §5). LastSeenAt is refreshed at most every five
/// minutes so reads stay cheap.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions,
    IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private static readonly TimeSpan LastSeenRefreshInterval = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var auth = authOptions.Value;
        var rawToken = Request.Cookies[auth.SessionCookieName];
        if (string.IsNullOrEmpty(rawToken)) return AuthenticateResult.NoResult();

        var db = Context.RequestServices.GetRequiredService<IIdentityDbContext>();
        var tokenHash = AuthTokens.Hash(rawToken);
        var session = await db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, Context.RequestAborted);

        var now = clock.UtcNow;
        if (session?.User is null || !session.IsActive(now, auth.SessionIdleTimeout))
        {
            return AuthenticateResult.NoResult();
        }

        if (now - session.LastSeenAt > LastSeenRefreshInterval)
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim(ClaimTypes.Name, session.User.DisplayName),
                new Claim(SessionAuthentication.SessionIdClaim, session.Id.ToString()),
            ],
            SessionAuthentication.Scheme);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SessionAuthentication.Scheme));
    }
}
