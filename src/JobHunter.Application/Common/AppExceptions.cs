namespace JobHunter.Application.Common;

/// <summary>Base for use-case failures mapped to ProblemDetails by the API layer.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Missing or not owned by the caller — always surfaced as 404, never 403 (spec 02 §5).</summary>
public sealed class NotFoundException(string code = "not_found", string message = "Resource not found.")
    : AppException(code, message);

public sealed class UnauthorizedAppException(string code, string message)
    : AppException(code, message);

public class ConflictException(string code, string message)
    : AppException(code, message);

/// <summary>An upstream service (AI, job board, Outreach Studio) failed for a reason the user can't fix — 502.</summary>
public sealed class UpstreamUnavailableException(string code, string message)
    : AppException(code, message);
