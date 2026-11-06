using JobHunter.Application.Analytics;

namespace JobHunter.UnitTests.Analytics;

/// <summary>Every definition of spec 03, Analytics, on hand-built rows with a fixed "now".</summary>
public class AnalyticsCalculatorTests
{
    /// <summary>A Thursday, noon UTC.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static AnalyticsWindow Window(int days = 90) => AnalyticsWindow.Ending(Now, days);

    private static OpportunityRow Row(
        double? appliedDaysAgo = null,
        double? answeredAfterDays = null,
        double? createdDaysAgo = null,
        string source = "manual",
        string title = "Backend Engineer",
        bool interviewing = false,
        bool offer = false)
    {
        var applied = appliedDaysAgo is { } a ? Now.AddDays(-a) : (DateTimeOffset?)null;
        var created = createdDaysAgo is { } c ? Now.AddDays(-c) : (applied ?? Now).AddDays(-1);
        var answered = applied is { } at && answeredAfterDays is { } after ? at.AddDays(after) : (DateTimeOffset?)null;
        return new OpportunityRow(Guid.NewGuid(), title, source, created, applied, answered, interviewing || offer, offer);
    }

    private static SummaryDto Summary(IEnumerable<OpportunityRow> rows, int days = 90, IReadOnlyList<string>? roles = null) =>
        AnalyticsCalculator.Summary(rows.ToList(), roles ?? [], Window(days), TimeZoneInfo.Utc);

    // Rates

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 4)]
    [InlineData(0, 0)]
    public void A_rate_under_five_is_withheld_but_keeps_its_counts(int numerator, int denominator)
    {
        var rate = Rate.Of(numerator, denominator);

        Assert.Null(rate.Value);
        Assert.Equal((numerator, denominator), (rate.Numerator, rate.Denominator));
    }

    [Theory]
    [InlineData(0, 5, 0.0)]
    [InlineData(1, 5, 0.2)]
    [InlineData(1, 3 + 3, 0.1667)]
    [InlineData(2, 6, 0.3333)]
    [InlineData(5, 5, 1.0)]
    public void From_five_on_a_rate_is_a_fraction_to_four_places(int numerator, int denominator, double expected) =>
        Assert.Equal(expected, Rate.Of(numerator, denominator).Value);

    // The range

    [Fact]
    public void The_range_is_the_last_days_up_to_now_with_both_ends_inside()
    {
        var window = Window(30);

        Assert.Equal(Now.AddDays(-30), window.From);
        Assert.True(window.Contains(window.From));
        Assert.True(window.Contains(Now));
        Assert.False(window.Contains(window.From.AddTicks(-1)));
        Assert.False(window.Contains(null));
    }

    // Saved and applications

    [Fact]
    public void Applications_are_counted_by_when_they_were_sent_and_saved_by_when_they_were_captured()
    {
        var summary = Summary(
        [
            Row(appliedDaysAgo: 10),                         // saved and sent in range
            Row(appliedDaysAgo: 10, createdDaysAgo: 200),    // sent in range, saved long before
            Row(appliedDaysAgo: 100),                        // sent before the range
            Row(createdDaysAgo: 5),                          // saved, not sent
        ]);

        Assert.Equal(2, summary.Applications);
        Assert.Equal(2, summary.Saved);
    }

    [Fact]
    public void Response_interview_and_offer_rates_are_over_the_applications()
    {
        var summary = Summary(
        [
            Row(appliedDaysAgo: 20, answeredAfterDays: 3, interviewing: true),
            Row(appliedDaysAgo: 20, answeredAfterDays: 3, offer: true),
            Row(appliedDaysAgo: 20, answeredAfterDays: 3),
            Row(appliedDaysAgo: 20),
            Row(appliedDaysAgo: 20),
            Row(appliedDaysAgo: 120, answeredAfterDays: 3, offer: true), // outside: not in any rate
        ]);

        Assert.Equal(new Rate(3, 5, 0.6), summary.Rates.Response);
        Assert.Equal(new Rate(2, 5, 0.4), summary.Rates.Interview);
        Assert.Equal(new Rate(1, 5, 0.2), summary.Rates.Offer);
    }

    [Fact]
    public void The_funnel_has_counts_and_only_the_cohort_steps_have_rates()
    {
        var summary = Summary(
        [
            Row(appliedDaysAgo: 20, answeredAfterDays: 2, offer: true),
            Row(appliedDaysAgo: 20, answeredAfterDays: 2, interviewing: true),
            Row(appliedDaysAgo: 20, answeredAfterDays: 2),
            Row(appliedDaysAgo: 20),
            Row(appliedDaysAgo: 20),
            Row(createdDaysAgo: 3),
            Row(createdDaysAgo: 3),
        ]);

        Assert.Equal(["saved", "applied", "responded", "interviewing", "offer"], summary.Funnel.Select(s => s.Step));
        Assert.Equal([7, 5, 3, 2, 1], summary.Funnel.Select(s => s.Count));
        Assert.Null(summary.Funnel[0].Rate);
        Assert.Null(summary.Funnel[1].Rate);
        Assert.Equal(summary.Rates.Response, summary.Funnel[2].Rate);
        Assert.Equal(summary.Rates.Interview, summary.Funnel[3].Rate);
        Assert.Equal(summary.Rates.Offer, summary.Funnel[4].Rate);
    }

    [Fact]
    public void No_response_only_looks_at_applications_sent_thirty_days_ago_or_more()
    {
        var summary = Summary(
        [
            Row(appliedDaysAgo: 30),                          // exactly 30 days: counts, no answer
            Row(appliedDaysAgo: 45),
            Row(appliedDaysAgo: 60),
            Row(appliedDaysAgo: 80, answeredAfterDays: 40),   // answered late: not "no response"
            Row(appliedDaysAgo: 85, answeredAfterDays: 1),
            Row(appliedDaysAgo: 29.9),                        // too recent to say
        ]);

        Assert.Equal(new Rate(3, 5, 0.6), summary.Rates.NoResponse);
    }

    [Fact]
    public void In_a_thirty_day_range_there_is_almost_nothing_old_enough_for_no_response()
    {
        var summary = Summary([Row(appliedDaysAgo: 5), Row(appliedDaysAgo: 29)], days: 30);

        Assert.Equal(new Rate(0, 0, null), summary.Rates.NoResponse);
    }

    // Time to first response

    [Fact]
    public void Time_to_first_response_is_the_median_of_the_responded_ones()
    {
        var applications = new[] { 1.0, 2, 3, 10, 30 }
            .Select(after => Row(appliedDaysAgo: 60, answeredAfterDays: after))
            .Append(Row(appliedDaysAgo: 60)) // no answer: left out, not counted as zero
            .ToList();

        Assert.Equal(new MedianDays(3, 5), AnalyticsCalculator.TimeToFirstResponse(applications));
    }

    [Fact]
    public void An_even_count_takes_the_middle_two_and_rounds_to_one_decimal()
    {
        var applications = new[] { 1.0, 2, 2, 4.5, 8, 9 }.Select(after => Row(appliedDaysAgo: 60, answeredAfterDays: after)).ToList();

        // (2 + 4.5) / 2 = 3.25, and halves round away from zero.
        Assert.Equal(new MedianDays(3.3, 6), AnalyticsCalculator.TimeToFirstResponse(applications));
    }

    [Fact]
    public void Under_five_answers_there_is_no_median_but_the_count_is_kept()
    {
        var applications = new[] { 1.0, 2, 3, 4 }.Select(after => Row(appliedDaysAgo: 60, answeredAfterDays: after)).ToList();

        Assert.Equal(new MedianDays(null, 4), AnalyticsCalculator.TimeToFirstResponse(applications));
    }

    // Weekly series

    [Fact]
    public void Every_week_that_overlaps_the_range_is_listed_from_monday_including_empty_ones()
    {
        // 30 days back from Thursday 5 November is Tuesday 6 October, in the week of Monday 5 October.
        var weeks = AnalyticsCalculator.Weekly([], Window(30), TimeZoneInfo.Utc);

        Assert.Equal(
            [new DateOnly(2026, 10, 5), new(2026, 10, 12), new(2026, 10, 19), new(2026, 10, 26), new(2026, 11, 2)],
            weeks.Select(w => w.WeekStart));
        Assert.All(weeks, w => Assert.Equal((0, 0), (w.Applications, w.Responses)));
        Assert.Equal([true, false, false, false, true], weeks.Select(w => w.Partial));
    }

    [Fact]
    public void A_range_starting_on_monday_midnight_has_a_whole_first_week()
    {
        var mondayMidnight = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var window = AnalyticsWindow.Ending(mondayMidnight.AddDays(28), 28);

        var weeks = AnalyticsCalculator.Weekly([], window, TimeZoneInfo.Utc);

        Assert.False(weeks[0].Partial);
        Assert.Equal(new DateOnly(2026, 11, 2), weeks[0].WeekStart);
    }

    [Fact]
    public void Answers_count_in_the_week_they_arrived_whatever_the_application_date()
    {
        var mondayNov2 = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        var sentLongAgo = new OpportunityRow(Guid.NewGuid(), "Dev", "manual", Now.AddDays(-200), Now.AddDays(-100), mondayNov2, false, false);
        var sentInRange = new OpportunityRow(Guid.NewGuid(), "Dev", "manual", Now.AddDays(-20), Now.AddDays(-15), null, false, false);

        var weeks = AnalyticsCalculator.Weekly([sentLongAgo, sentInRange], Window(30), TimeZoneInfo.Utc);

        Assert.Equal(1, weeks.Single(w => w.WeekStart == new DateOnly(2026, 11, 2)).Responses);
        Assert.Equal(1, weeks.Sum(w => w.Applications)); // the one sent 100 days ago isn't in the range
        Assert.Equal(1, weeks.Single(w => w.WeekStart == new DateOnly(2026, 10, 19)).Applications); // 21 October
    }

    [Fact]
    public void Weeks_follow_the_viewers_time_zone()
    {
        // Monday 2 November, 03:00 UTC is still Sunday 1 November, 22:00 in New York.
        var sent = new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);
        var row = new OpportunityRow(Guid.NewGuid(), "Dev", "manual", sent.AddDays(-1), sent, null, false, false);
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        var utcWeeks = AnalyticsCalculator.Weekly([row], Window(30), TimeZoneInfo.Utc);
        var localWeeks = AnalyticsCalculator.Weekly([row], Window(30), newYork);

        Assert.Equal(new DateOnly(2026, 11, 2), utcWeeks.Single(w => w.Applications == 1).WeekStart);
        Assert.Equal(new DateOnly(2026, 10, 26), localWeeks.Single(w => w.Applications == 1).WeekStart);
    }

    [Fact]
    public void A_year_has_fifty_three_weekly_buckets_when_it_spans_them()
    {
        var weeks = AnalyticsCalculator.Weekly([], Window(365), TimeZoneInfo.Utc);

        Assert.Equal(53, weeks.Count);
        Assert.Equal(new DateOnly(2026, 11, 2), weeks[^1].WeekStart);
        Assert.All(weeks.Zip(weeks.Skip(1)), pair => Assert.Equal(7, pair.Second.WeekStart.DayNumber - pair.First.WeekStart.DayNumber));
    }

    // By source

    [Fact]
    public void Sources_with_applications_are_listed_most_first_with_their_rates()
    {
        var rows = Enumerable.Range(0, 5).Select(i => Row(appliedDaysAgo: 10, answeredAfterDays: i < 2 ? 1 : null, source: "greenhouse", interviewing: i == 0))
            .Concat([Row(appliedDaysAgo: 10, source: "lever"), Row(appliedDaysAgo: 10, source: "ashby")])
            .Append(Row(createdDaysAgo: 2, source: "url")); // saved only: no row

        var bySource = Summary(rows).BySource;

        Assert.Equal(["greenhouse", "ashby", "lever"], bySource.Select(s => s.Key));
        var greenhouse = bySource[0];
        Assert.Equal((5, 2, 1), (greenhouse.Applications, greenhouse.Responses, greenhouse.Interviews));
        Assert.Equal(0.4, greenhouse.ResponseRate.Value);
        Assert.Equal(0.2, greenhouse.InterviewRate.Value);
        Assert.Null(bySource[1].ResponseRate.Value); // one application: too few to say
    }

    // By target role

    [Fact]
    public void An_application_belongs_to_the_first_role_whose_every_word_is_in_its_title()
    {
        var rows = new[]
        {
            Row(appliedDaysAgo: 5, title: "Senior Backend Engineer"),
            Row(appliedDaysAgo: 5, title: "Backend Platform Engineer"),
            Row(appliedDaysAgo: 5, title: "Engineer, Backend (Payments)"),
            Row(appliedDaysAgo: 5, title: "Platform Engineer"),
            Row(appliedDaysAgo: 5, title: "Product Designer"),
        };

        var byRole = Summary(rows, roles: ["Backend Engineer", "Platform Engineer", "Data Engineer"]).ByRole;

        Assert.Equal(["Backend Engineer", "Platform Engineer", "Data Engineer", null], byRole.Select(r => r.Key));
        Assert.Equal([3, 1, 0, 1], byRole.Select(r => r.Applications)); // "Backend Platform Engineer" went to the first
        Assert.Equal(rows.Length, byRole.Sum(r => r.Applications));
    }

    [Fact]
    public void Role_matching_ignores_case_accents_and_punctuation()
    {
        var byRole = Summary(
            [Row(appliedDaysAgo: 5, title: "INGENIERO DE SOFTWARE SÉNIOR"), Row(appliedDaysAgo: 5, title: "C#/.NET Developer")],
            roles: ["Ingeniero de software senior", ".NET developer"]).ByRole;

        Assert.Equal([1, 1], byRole.Select(r => r.Applications));
        Assert.DoesNotContain(byRole, r => r.Key is null); // nothing left for "other"
    }

    [Fact]
    public void Without_target_roles_everything_is_other_and_without_applications_roles_still_show()
    {
        Assert.Equal([(null, 2)], Summary([Row(appliedDaysAgo: 5), Row(appliedDaysAgo: 6)]).ByRole.Select(r => (r.Key, r.Applications)));
        Assert.Equal([("Backend Engineer", 0)], Summary([], roles: ["Backend Engineer"]).ByRole.Select(r => (r.Key, r.Applications)));
    }

    [Fact]
    public void A_role_with_no_words_matches_nothing()
    {
        var byRole = Summary([Row(appliedDaysAgo: 5)], roles: ["—"]).ByRole;

        Assert.Equal([("—", 0), (null, 1)], byRole.Select(r => (r.Key, r.Applications)));
    }

    [Theory]
    [InlineData("C# / .NET Developer", new[] { "c", "net", "developer" })]
    [InlineData("Ingeniería de Datos", new[] { "ingenieria", "de", "datos" })]
    [InlineData("  Back-end  Engineer II ", new[] { "back", "end", "engineer", "ii" })]
    [InlineData("", new string[0])]
    public void Words_are_runs_of_letters_and_digits_lowercased_without_accents(string text, string[] expected) =>
        Assert.Equal(expected, AnalyticsCalculator.Words(text));

    // Skills

    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static string Name(string key) => key.ToUpperInvariant();

    [Fact]
    public void Each_posting_counts_once_per_skill_and_as_a_must_if_any_row_is()
    {
        RequirementRow[] requirements =
        [
            new(A, "csharp", "must"), new(A, "csharp", "nice"), // twice in one posting: one posting, a must
            new(B, "csharp", "nice"),
            new(C, "csharp", "must"),
            new(A, "postgresql", "nice"),
        ];

        var skills = AnalyticsCalculator.Skills(90, requirements, [], new HashSet<string> { "csharp" }, Name);

        Assert.Equal(3, skills.Postings);
        Assert.Equal(new RequestedSkillDto("csharp", "CSHARP", 3, 2, 1, true), skills.Requested[0]);
        Assert.Equal(new RequestedSkillDto("postgresql", "POSTGRESQL", 1, 0, 1, false), skills.Requested[1]);
    }

    [Fact]
    public void Requested_skills_are_ordered_by_postings_then_name_and_capped_at_fifteen()
    {
        var requirements = Enumerable.Range(0, 20)
            .Select(i => new RequirementRow(i < 2 ? A : B, $"skill{i:00}", "must"))
            .Append(new RequirementRow(C, "skill19", "must"))
            .ToList();

        var requested = AnalyticsCalculator.Skills(90, requirements, [], new HashSet<string>(), Name).Requested;

        Assert.Equal(AnalyticsRules.TopRequested, requested.Count);
        Assert.Equal("skill19", requested[0].Key);
        Assert.Equal(["skill00", "skill01", "skill02"], requested.Skip(1).Take(3).Select(r => r.Key));
    }

    [Fact]
    public void Gaps_are_tallied_by_posting_and_capped_at_ten()
    {
        var gaps = Enumerable.Range(0, 12).Select(i => new GapRow(A, $"gap{i:00}", "nice"))
            .Append(new GapRow(B, "gap05", "must"))
            .Append(new GapRow(C, "gap05", "nice"))
            .ToList();

        var result = AnalyticsCalculator.Skills(90, [], gaps, new HashSet<string>(), Name).Gaps;

        Assert.Equal(AnalyticsRules.TopGaps, result.Count);
        Assert.Equal(new GapDto("gap05", "GAP05", 3, 1, 2), result[0]);
    }

    [Fact]
    public void Without_confirmed_requirements_there_is_nothing_to_rank()
    {
        var skills = AnalyticsCalculator.Skills(30, [], [], new HashSet<string>(), Name);

        Assert.Equal((30, 0), (skills.Days, skills.Postings));
        Assert.Empty(skills.Requested);
        Assert.Empty(skills.Gaps);
    }

    // Next up

    [Fact]
    public void Next_up_is_by_priority_then_fit_with_unscored_last_then_oldest()
    {
        NextUpItemDto Item(string name, string priority, int? score, int daysOld) =>
            new(Guid.NewGuid(), name, "Contoso", "saved", priority, score, score is null ? null : "good", Now.AddDays(-daysOld));

        var ordered = AnalyticsCalculator.NextUpOrder(
        [
            Item("low strong", "low", 95, 1),
            Item("normal unscored", "normal", null, 9),
            Item("normal 60", "normal", 60, 1),
            Item("normal 80 newer", "normal", 80, 1),
            Item("normal 80 older", "normal", 80, 5),
            Item("high weak", "high", 20, 1),
        ]);

        Assert.Equal(
            ["high weak", "normal 80 older", "normal 80 newer", "normal 60", "normal unscored", "low strong"],
            ordered.Select(i => i.Title));
    }
}
