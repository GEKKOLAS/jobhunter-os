using JobHunter.Application.Identity;
using JobHunter.Domain.Identity;

namespace JobHunter.UnitTests.Identity;

public class SessionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Idle = TimeSpan.FromDays(7);

    private static UserSession Session(DateTimeOffset lastSeen, DateTimeOffset expires) => new()
    {
        UserId = Guid.NewGuid(),
        TokenHash = [1],
        CreatedAt = lastSeen,
        LastSeenAt = lastSeen,
        ExpiresAt = expires,
    };

    [Fact]
    public void A_session_used_recently_and_not_expired_is_active()
    {
        Assert.True(Session(Now.AddDays(-1), Now.AddDays(20)).IsActive(Now, Idle));
    }

    [Fact]
    public void A_session_idle_longer_than_the_window_is_not_active()
    {
        Assert.False(Session(Now.AddDays(-7), Now.AddDays(20)).IsActive(Now, Idle));
    }

    [Fact]
    public void A_session_past_its_absolute_expiry_is_not_active_however_recently_used()
    {
        Assert.False(Session(Now.AddMinutes(-1), Now).IsActive(Now, Idle));
    }

    [Fact]
    public void A_reset_token_is_usable_once_and_only_before_it_expires()
    {
        var token = new PasswordResetToken { UserId = Guid.NewGuid(), TokenHash = [1], ExpiresAt = Now.AddMinutes(30) };
        Assert.True(token.IsUsable(Now));
        Assert.False(token.IsUsable(Now.AddMinutes(30)));

        token.UsedAt = Now;
        Assert.False(token.IsUsable(Now));
    }

    [Fact]
    public void Tokens_are_url_safe_random_and_hashed_deterministically()
    {
        var (raw1, hash1) = AuthTokens.Create();
        var (raw2, _) = AuthTokens.Create();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", raw1);
        Assert.NotEqual(raw1, raw2);
        Assert.Equal(hash1, AuthTokens.Hash(raw1));
        Assert.Equal(32, hash1.Length);
    }
}
