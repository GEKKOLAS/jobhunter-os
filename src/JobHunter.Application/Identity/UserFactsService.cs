using JobHunter.Application.Common;
using JobHunter.Application.Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Identity;

internal sealed class UserFactsService(IIdentityDbContext db, ICurrentUser currentUser) : IUserFacts
{
    public async Task<string> DisplayNameAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "";
}
