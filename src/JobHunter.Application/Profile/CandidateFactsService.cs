using JobHunter.Application.Common;
using JobHunter.Application.Profile.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Profile;

internal sealed class CandidateFactsService(IProfileDbContext db, ICurrentUser currentUser) : ICandidateFacts
{
    public async Task<CandidateFacts> ForCurrentUserAsync(CancellationToken ct)
    {
        var skills = await db.ProfileSkills.AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .OrderBy(s => s.Name)
            .Select(s => new CandidateSkill(s.Key, s.Name, s.Level, s.Years))
            .ToListAsync(ct);

        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
        return new CandidateFacts(skills, profile?.Seniority, profile?.WorkModes ?? [], profile?.Location ?? "");
    }
}
