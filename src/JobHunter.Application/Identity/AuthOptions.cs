namespace JobHunter.Application.Identity;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    // Cookies are scoped by host, not port: Mail Template Hub on localhost:3000 uses
    // mth_*, so these names must differ or the two apps sign each other out.
    public string SessionCookieName { get; init; } = "jh_session";
    public string CsrfCookieName { get; init; } = "jh_csrf";
    public string CsrfHeaderName { get; init; } = "X-CSRF-Token";

    /// <summary>True in production (with a <c>__Host-</c> cookie name); false for http in development.</summary>
    public bool SecureCookies { get; init; }

    public int SessionAbsoluteDays { get; init; } = 30;
    public int SessionIdleDays { get; init; } = 7;
    public int ResetTokenMinutes { get; init; } = 30;

    /// <summary>Development only: write password-reset links to the log (there is no email in v1).</summary>
    public bool LogPasswordResetLinks { get; init; }

    /// <summary>Where reset links point: the web app's origin.</summary>
    public string WebBaseUrl { get; init; } = "http://localhost:3100";

    public TimeSpan SessionAbsoluteLifetime => TimeSpan.FromDays(SessionAbsoluteDays);
    public TimeSpan SessionIdleTimeout => TimeSpan.FromDays(SessionIdleDays);
}
