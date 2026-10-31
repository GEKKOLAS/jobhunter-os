namespace JobHunter.Domain.Common;

/// <summary>
/// Invariant violation raised by domain code. Carries a machine-readable code that the API
/// maps to a ProblemDetails <c>errorCode</c> (spec 03, conventions).
/// </summary>
public class DomainException(string code, string? message = null)
    : Exception(message ?? code)
{
    public string Code { get; } = code;
}
