using JobHunter.Domain.Matching;
using static JobHunter.Domain.Matching.RequirementVocabulary;

namespace JobHunter.UnitTests.Matching;

/// <summary>Algorithm v1, rule by rule (spec 03, Matching). A failing test here means the score changed for everyone.</summary>
public class FitAlgorithmTests
{
    private static RequirementInput Req(string key, string kind = Must, int? years = null, string? family = null) =>
        new($"Needs {key}", kind, key, key, years, family);

    private static RequirementInput NoSkill(string text = "A degree", string kind = Must) => new(text, kind, null, null, null, null);

    private static SkillInput Skill(string key, int level = 4, decimal years = 5, string? family = null) => new(key, key, level, years, family);

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    // Every level against the years rule: level 3 is the line, and either shortfall is half.
    [Theory]
    [InlineData(1, 5, null, "partial", "under_level")]
    [InlineData(2, 5, null, "partial", "under_level")]
    [InlineData(3, 5, null, "covered", "meets")]
    [InlineData(4, 5, null, "covered", "meets")]
    [InlineData(5, 5, null, "covered", "meets")]
    [InlineData(3, 5, 5, "covered", "meets")]
    [InlineData(3, 4.9, 5, "partial", "under_years")]
    [InlineData(5, 0, 1, "partial", "under_years")]
    [InlineData(2, 5, 5, "partial", "under_level")]
    [InlineData(2, 4.9, 5, "partial", "under_level_and_years")]
    [InlineData(1, 0, 40, "partial", "under_level_and_years")]
    [InlineData(3, 0, 0, "covered", "meets")]
    public void A_skill_you_have_is_covered_or_half_covered(int level, double years, int? yearsMin, string coverage, string reason)
    {
        var result = FitAlgorithm.Assess([Req("csharp", years: yearsMin)], [Skill("csharp", level, (decimal)years)]);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal((coverage, reason), (outcome.Coverage, outcome.Reason));
        Assert.Equal(level, outcome.YourLevel);
        Assert.Equal(years, outcome.YourYears);
        Assert.Equal(coverage == "covered" ? 100 : 50, result.Score);
    }

    [Fact]
    public void A_skill_from_the_same_family_is_half_covered_and_named()
    {
        var result = FitAlgorithm.Assess([Req("java", family: "jvm")], [Skill("kotlin", family: "jvm")]);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(("partial", "related", "kotlin"), (outcome.Coverage, outcome.Reason, outcome.RelatedSkill));
        Assert.Null(outcome.YourLevel);
        Assert.Equal(50, result.Score);
    }

    [Fact]
    public void The_skill_itself_beats_a_relative_even_when_it_is_weak()
    {
        var result = FitAlgorithm.Assess([Req("java", family: "jvm")], [Skill("kotlin", 5, family: "jvm"), Skill("java", 1, family: "jvm")]);

        Assert.Equal("under_level", Assert.Single(result.Outcomes).Reason);
    }

    [Theory]
    [InlineData(null, "dotnet")] // the requirement has no family
    [InlineData("jvm", null)] // yours has none: null never equals null here
    [InlineData("jvm", "dotnet")]
    public void No_shared_family_is_a_gap(string? wanted, string? yours)
    {
        var result = FitAlgorithm.Assess([Req("java", family: wanted)], [Skill("csharp", family: yours)]);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(("gap", "missing"), (outcome.Coverage, outcome.Reason));
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Must_weighs_three_times_nice()
    {
        // Covered must (3 of 3) and a missing nice (0 of 1): 3 of 4.
        Assert.Equal(75, FitAlgorithm.Assess([Req("a"), Req("b", Nice)], [Skill("a")]).Score);
        // Covered nice (1 of 1) and a missing must (0 of 3): 1 of 4.
        Assert.Equal(25, FitAlgorithm.Assess([Req("a"), Req("b", Nice)], [Skill("b")]).Score);
        // A half-covered must and a covered nice: 2.5 of 4.
        Assert.Equal(63, FitAlgorithm.Assess([Req("a"), Req("b", Nice)], [Skill("a", level: 1), Skill("b")]).Score);
    }

    [Fact]
    public void Halves_round_away_from_zero()
    {
        // 2.5 of 4 is 62.5, which bankers' rounding would make 62.
        Assert.Equal(63, FitAlgorithm.Assess([Req("a"), Req("b", Nice)], [Skill("a", level: 2), Skill("b")]).Score);
    }

    [Fact]
    public void Requirements_without_a_skill_are_listed_but_never_move_the_score()
    {
        var with = FitAlgorithm.Assess([Req("a"), NoSkill()], [Skill("a")]);

        Assert.Equal(100, with.Score);
        Assert.Equal(("not_scored", "no_skill"), (with.Outcomes[1].Coverage, with.Outcomes[1].Reason));
        Assert.Equal("A degree", with.Outcomes[1].Text);
    }

    [Fact]
    public void Outcomes_keep_the_requirements_order()
    {
        var result = FitAlgorithm.Assess([Req("b"), NoSkill(), Req("a", Nice)], [Skill("a")]);

        Assert.Equal(["b", null, "a"], result.Outcomes.Select(o => o.SkillKey));
    }

    [Fact]
    public void No_requirements_means_no_score_not_zero()
    {
        var result = FitAlgorithm.Assess([], [Skill("a")]);

        Assert.Equal((null, null, FitAlgorithm.NoRequirements), (result.Score, result.Band, result.MissingReason));
    }

    [Fact]
    public void Only_unscorable_requirements_means_no_score()
    {
        var result = FitAlgorithm.Assess([NoSkill(), NoSkill("Fluent English")], [Skill("a")]);

        Assert.Equal((null, null, FitAlgorithm.NoScoredRequirements), (result.Score, result.Band, result.MissingReason));
        Assert.Equal(2, result.Outcomes.Count);
    }

    [Fact]
    public void An_empty_profile_means_no_score_not_zero()
    {
        var result = FitAlgorithm.Assess([Req("a")], []);

        Assert.Equal((null, null, FitAlgorithm.NoSkills), (result.Score, result.Band, result.MissingReason));
    }

    [Theory]
    [InlineData(100, "strong")]
    [InlineData(75, "strong")]
    [InlineData(74, "good")]
    [InlineData(55, "good")]
    [InlineData(54, "partial")]
    [InlineData(35, "partial")]
    [InlineData(34, "weak")]
    [InlineData(0, "weak")]
    public void Bands_have_exact_edges(int score, string band) => Assert.Equal(band, FitAlgorithm.BandOf(score));

    [Theory]
    [InlineData("strong", "saved", 0, "high")]
    [InlineData("strong", "preparing", 13, "high")]
    [InlineData("strong", "saved", 14, "normal")] // no longer "less than 14 days"
    [InlineData("strong", "applied", 1, "normal")] // already moving: nothing to prioritize
    [InlineData("strong", "interviewing", 1, "normal")]
    [InlineData("good", "saved", 1, "normal")]
    [InlineData("partial", "saved", 1, "normal")]
    [InlineData("weak", "saved", 1, "low")]
    [InlineData("weak", "applied", 100, "low")]
    [InlineData(null, "saved", 1, null)]
    public void Suggested_priority_follows_the_band_stage_and_age(string? band, string stage, int ageDays, string? priority) =>
        Assert.Equal(priority, FitAlgorithm.SuggestedPriority(band, stage, Now.AddDays(-ageDays), Now));

    [Fact]
    public void Suggested_priority_is_exact_about_fourteen_days()
    {
        Assert.Equal("high", FitAlgorithm.SuggestedPriority("strong", "saved", Now.AddDays(-14).AddSeconds(1), Now));
        Assert.Equal("normal", FitAlgorithm.SuggestedPriority("strong", "saved", Now.AddDays(-14), Now));
    }

    [Fact]
    public void A_work_mode_outside_your_preferences_is_noted()
    {
        var note = Assert.Single(FitAlgorithm.Notes("Engineer", null, "onsite", null, ["remote", "hybrid"], ""));

        Assert.Equal(("work_mode", "onsite", "remote, hybrid"), (note.Code, note.Posting, note.Profile));
    }

    [Theory]
    [InlineData(null, new[] { "remote" })] // the posting doesn't say
    [InlineData("onsite", new string[0])] // you didn't say
    [InlineData("remote", new[] { "remote", "hybrid" })] // it matches
    public void No_work_mode_note_without_a_mismatch(string? posting, string[] preferred) =>
        Assert.Empty(FitAlgorithm.Notes("Engineer", null, posting, null, preferred, ""));

    [Theory]
    [InlineData("San Francisco Bay Area", null, "Bogotá, Colombia", true)]
    [InlineData("Medellín, Colombia", "onsite", "Bogotá, Colombia", false)] // a shared word: same country
    [InlineData("Bogota", null, "Bogotá", false)] // accents don't matter
    [InlineData("San Francisco", "remote", "Bogotá", false)] // remote: anywhere goes
    [InlineData("Remote - Canada", null, "Bogotá", false)]
    [InlineData("Remoto", null, "Bogotá", false)]
    [InlineData(null, null, "Bogotá", false)]
    [InlineData("San Francisco", null, "", false)]
    [InlineData("Bay Area", null, "Bay City", false)] // nothing but generic words: nothing to compare
    public void A_location_is_noted_only_with_nothing_in_common(string? posting, string? workMode, string profile, bool noted)
    {
        var notes = FitAlgorithm.Notes("Engineer", posting, workMode, null, [], profile);

        Assert.Equal(noted, notes.Any(n => n.Code == "location"));
    }

    [Theory]
    [InlineData("Senior Backend Engineer", "senior")]
    [InlineData("Sr. Backend Engineer", "senior")]
    [InlineData("Sr Backend Engineer", "senior")]
    [InlineData("Ingeniero Sénior", "senior")]
    [InlineData("Junior Developer", "junior")]
    [InlineData("Jr. Developer", "junior")]
    [InlineData("Entry-level Analyst", "junior")]
    [InlineData("Mid-level Engineer", "mid")]
    [InlineData("Semi Senior Developer", "mid")]
    [InlineData("Tech Lead", "lead")]
    [InlineData("Staff Engineer", "lead")]
    [InlineData("Principal Engineer", "principal")]
    [InlineData("Backend Engineer", null)]
    [InlineData("Leadership Coach", null)]
    [InlineData("Seniority Analyst", null)]
    [InlineData("Non-senior Engineer", null)]
    public void Seniority_comes_from_the_title_only_when_stated(string title, string? seniority) =>
        Assert.Equal(seniority, FitAlgorithm.SeniorityInTitle(title));

    [Fact]
    public void A_seniority_mismatch_is_noted_and_a_match_is_not()
    {
        var note = Assert.Single(FitAlgorithm.Notes("Senior Engineer", null, null, "mid", [], ""));

        Assert.Equal(("seniority", "senior", "mid"), (note.Code, note.Posting, note.Profile));
        Assert.Empty(FitAlgorithm.Notes("Senior Engineer", null, null, "senior", [], ""));
        Assert.Empty(FitAlgorithm.Notes("Senior Engineer", null, null, null, [], ""));
    }
}
