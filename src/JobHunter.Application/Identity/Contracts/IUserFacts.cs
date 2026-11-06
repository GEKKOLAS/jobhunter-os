namespace JobHunter.Application.Identity.Contracts;

/// <summary>What another module may know about the signed-in user themselves.</summary>
public interface IUserFacts
{
    Task<string> DisplayNameAsync(CancellationToken ct);
}
