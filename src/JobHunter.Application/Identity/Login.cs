using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Application.Identity;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IRequestContext requestContext,
    IOptions<AuthOptions> authOptions,
    IValidator<LoginCommand> validator,
    IClock clock)
{
    // Argon2id hash of a random throwaway password, verified when the account doesn't
    // exist so "unknown email" and "wrong password" take comparable time.
    private const string DummyHash =
        "$argon2id$v=19$m=65536,t=3,p=2$AAAAAAAAAAAAAAAAAAAAAA$m0kNXmn3aSAJrLd7RS9GLpSBCPGxDvPYRw2FVFyRj/g";

    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        var email = command.Email.Trim();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            passwordHasher.Verify(command.Password, DummyHash);
            throw InvalidCredentials();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            audit.Add(IdentityAudit.LoginFailed, user.Id, "user", user.Id);
            await db.SaveChangesAsync(ct);
            throw InvalidCredentials();
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = passwordHasher.Hash(command.Password);
        }

        var now = clock.UtcNow;
        var (rawToken, tokenHash) = AuthTokens.Create();
        db.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            Ip = requestContext.Ip,
            UserAgent = requestContext.UserAgent,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + authOptions.Value.SessionAbsoluteLifetime,
        });

        audit.Add(IdentityAudit.Login, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);

        return new AuthResult(
            new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt), rawToken, AuthTokens.CreateCsrfToken());
    }

    private static UnauthorizedAppException InvalidCredentials() =>
        new("auth.invalid_credentials", "Invalid email or password.");
}
