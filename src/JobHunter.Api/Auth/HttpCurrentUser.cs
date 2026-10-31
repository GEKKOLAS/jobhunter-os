using System.Security.Claims;
using JobHunter.Application.Common;

namespace JobHunter.Api.Auth;

/// <summary><see cref="ICurrentUser"/> and <see cref="IRequestContext"/> backed by the current HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser, IRequestContext
{
    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context.");

    public Guid UserId =>
        Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Caller is not authenticated.");

    public Guid SessionId =>
        Guid.TryParse(Context.User.FindFirstValue(SessionAuthentication.SessionIdClaim), out var id)
            ? id
            : throw new InvalidOperationException("Caller is not authenticated.");

    // Null in background jobs, so audit writes work there too.
    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(value) ? null : value[..Math.Min(value.Length, 500)];
        }
    }
}
