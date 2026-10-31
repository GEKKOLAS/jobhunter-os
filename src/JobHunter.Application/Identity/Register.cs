using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Application.Identity;

public sealed record RegisterCommand(string Email, string Password, string DisplayName);

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public sealed class RegisterHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IRequestContext requestContext,
    IOptions<AuthOptions> authOptions,
    IValidator<RegisterCommand> validator,
    IClock clock)
{
    public async Task<AuthResult> HandleAsync(RegisterCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        var email = command.Email.Trim();

        // Hash before the existence check so a duplicate and a fresh registration take
        // comparable time: the response must not reveal whether an address is registered.
        var passwordHash = passwordHasher.Hash(command.Password);

        var now = clock.UtcNow;
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            // Same response shape, but no session.
            var decoy = new UserDto(Guid.CreateVersion7(), email, command.DisplayName.Trim(), now);
            return new AuthResult(decoy, SessionToken: null, CsrfToken: null);
        }

        var user = new User { Email = email, PasswordHash = passwordHash, DisplayName = command.DisplayName.Trim() };
        db.Users.Add(user);

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

        audit.Add(IdentityAudit.Register, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);

        return new AuthResult(new UserDto(user.Id, user.Email, user.DisplayName, now), rawToken, AuthTokens.CreateCsrfToken());
    }
}
