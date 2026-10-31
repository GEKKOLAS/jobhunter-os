using JobHunter.Domain.Common;
using JobHunter.Domain.Opportunities;

namespace JobHunter.UnitTests.Opportunities;

/// <summary>The "Allowed moves" table of spec 03, checked for every pair of stages.</summary>
public class StageRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Expected allowed moves from each open stage (previous stage irrelevant while open).
    private static readonly Dictionary<string, string[]> FromOpen = new()
    {
        ["saved"] = ["preparing", "applied", "screening", "interviewing", "offer", "rejected", "withdrawn"],
        ["preparing"] = ["saved", "applied", "screening", "interviewing", "offer", "rejected", "withdrawn"],
        ["applied"] = ["preparing", "screening", "interviewing", "offer", "rejected", "withdrawn", "no_response"],
        ["screening"] = ["applied", "interviewing", "offer", "rejected", "withdrawn", "no_response"],
        ["interviewing"] = ["screening", "offer", "rejected", "withdrawn", "no_response"],
        ["offer"] = ["interviewing", "accepted", "declined", "rejected", "withdrawn"],
    };

    public static TheoryData<string, string> EveryPair()
    {
        var data = new TheoryData<string, string>();
        foreach (var from in Stages.Open)
        foreach (var to in Stages.All)
        {
            data.Add(from, to);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Open_stages_allow_exactly_the_moves_in_the_table(string from, string to)
    {
        var expected = FromOpen[from].Contains(to);
        Assert.Equal(expected, StageRules.CanMove(from, previous: null, to));
    }

    [Fact]
    public void A_closed_stage_can_only_go_back_where_it_came_from()
    {
        Assert.Equal(["applied"], StageRules.AllowedMoves("rejected", previous: "applied"));
        Assert.Equal(["offer"], StageRules.AllowedMoves("accepted", previous: "offer"));
        Assert.Empty(StageRules.AllowedMoves("withdrawn", previous: null));
        Assert.Empty(StageRules.AllowedMoves("withdrawn", previous: "rejected"));
    }

    [Fact]
    public void Starting_past_applied_stamps_applied_but_not_a_response()
    {
        var o = New();
        o.Start("applied", T0);

        Assert.Equal(T0, o.AppliedAt);
        Assert.Null(o.FirstResponseAt);
        Assert.Null(o.ClosedAt);
    }

    [Fact]
    public void Skipping_ahead_stamps_both_applied_and_response()
    {
        var o = New();
        o.Start("saved", T0);
        o.MoveTo("interviewing", T0.AddDays(3), note: "Recruiter reached out");

        Assert.Equal(T0.AddDays(3), o.AppliedAt);
        Assert.Equal(T0.AddDays(3), o.FirstResponseAt);
    }

    [Fact]
    public void A_rejection_after_applying_counts_as_a_response_but_no_response_does_not()
    {
        var rejected = New();
        rejected.Start("applied", T0);
        rejected.MoveTo("rejected", T0.AddDays(5), null);

        var silent = New();
        silent.Start("applied", T0);
        silent.MoveTo("no_response", T0.AddDays(30), null);

        var neverApplied = New();
        neverApplied.Start("saved", T0);
        neverApplied.MoveTo("rejected", T0.AddDays(1), null);

        Assert.Equal(T0.AddDays(5), rejected.FirstResponseAt);
        Assert.Null(silent.FirstResponseAt);
        Assert.Null(neverApplied.FirstResponseAt);
        Assert.Null(neverApplied.AppliedAt);
    }

    [Fact]
    public void Undoing_a_close_clears_closed_at_but_keeps_what_happened()
    {
        var o = New();
        o.Start("applied", T0);
        o.MoveTo("screening", T0.AddDays(2), null);
        o.MoveTo("rejected", T0.AddDays(4), null);
        Assert.Equal(T0.AddDays(4), o.ClosedAt);

        var undo = o.MoveTo("screening", T0.AddDays(4.1), "Marked by mistake");

        Assert.Equal("screening", o.Stage);
        Assert.Null(o.ClosedAt);
        Assert.Equal(T0, o.AppliedAt);
        Assert.Equal(T0.AddDays(2), o.FirstResponseAt);
        Assert.Equal(("rejected", "screening", "Marked by mistake"), (undo.FromStage, undo.ToStage, undo.Note));
    }

    [Fact]
    public void Illegal_moves_and_closed_starts_throw()
    {
        var o = New();
        o.Start("saved", T0);

        Assert.Equal("opportunity.invalid_stage", Assert.Throws<DomainException>(() => o.MoveTo("accepted", T0, null)).Code);
        Assert.Throws<DomainException>(() => New().Start("rejected", T0));
    }

    [Theory]
    [InlineData("https://www.Contoso.com/careers", "contoso.com")]
    [InlineData("contoso.com", "contoso.com")]
    [InlineData("http://jobs.fabrikam.io", "jobs.fabrikam.io")]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    public void Company_domains_come_from_the_website_host(string website, string? domain)
    {
        Assert.Equal(domain, Company.DomainOf(website));
    }

    private static Opportunity New() => new() { UserId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Title = "Engineer", Source = "manual" };
}
