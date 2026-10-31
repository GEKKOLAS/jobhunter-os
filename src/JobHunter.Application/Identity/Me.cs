using FluentValidation;
using JobHunter.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Identity;

public sealed record UpdateMeCommand(string DisplayName);

public sealed class UpdateMeValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class MeHandler(
    IIdentityDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    IAuditWriter audit,
    IValidator<UpdateMeCommand> updateValidator,
    IValidator<ChangePasswordCommand> passwordValidator)
{
    public async Task<UserDto> GetAsync(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new NotFoundException();
        return new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
    }

    public async Task<UserDto> UpdateAsync(UpdateMeCommand command, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new NotFoundException();
        user.DisplayName = command.DisplayName.Trim();
        audit.Add(IdentityAudit.ProfileUpdated, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
    }

    public async Task ChangePasswordAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        await passwordValidator.ValidateAndThrowAsync(command, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw new NotFoundException();
        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAppException("auth.wrong_password", "Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.Hash(command.NewPassword);

        // Keep the session that made the change; end every other one.
        await db.UserSessions
            .Where(s => s.UserId == currentUser.UserId && s.Id != currentUser.SessionId)
            .ExecuteDeleteAsync(ct);

        audit.Add(IdentityAudit.PasswordChanged, user.Id, "user", user.Id);
        await db.SaveChangesAsync(ct);
    }
}
