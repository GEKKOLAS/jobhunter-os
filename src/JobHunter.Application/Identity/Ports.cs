namespace JobHunter.Application.Identity;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);

    /// <summary>True when the stored hash uses outdated parameters; rehashed on the next login.</summary>
    bool NeedsRehash(string encodedHash);
}

/// <summary>Delivers password-reset links. v1 has no email: development logs the link.</summary>
public interface IPasswordResetNotifier
{
    Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken);
}
