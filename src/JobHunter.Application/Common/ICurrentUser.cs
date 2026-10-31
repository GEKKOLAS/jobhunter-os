namespace JobHunter.Application.Common;

/// <summary>Identity of the authenticated caller, populated by the API's session handler.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SessionId { get; }
}

/// <summary>Request metadata recorded with audit entries; null outside an HTTP request.</summary>
public interface IRequestContext
{
    string? Ip { get; }
    string? UserAgent { get; }
}
