using System.Security.Cryptography;
using System.Text;

namespace JobHunter.Application.Identity;

public sealed record UserDto(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record SessionDto(Guid Id, string? Ip, string? UserAgent, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Current);

/// <summary>Result of register/login. The API layer puts the tokens in cookies; they never reach JSON except the CSRF one.</summary>
public sealed record AuthResult(UserDto User, string? SessionToken, string? CsrfToken);

public static class AuthTokens
{
    /// <summary>A random 256-bit token; only its SHA-256 goes to the database.</summary>
    public static (string Raw, byte[] Hash) Create()
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    public static byte[] Hash(string raw) => SHA256.HashData(Encoding.UTF8.GetBytes(raw));

    public static string CreateCsrfToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Audit action codes of the Identity module, stored as text.</summary>
public static class IdentityAudit
{
    public const string Register = "auth.register";
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string PasswordReset = "auth.password_reset";
    public const string PasswordChanged = "auth.password_changed";
    public const string SessionRevoked = "auth.session_revoked";
    public const string ProfileUpdated = "user.updated";
}
