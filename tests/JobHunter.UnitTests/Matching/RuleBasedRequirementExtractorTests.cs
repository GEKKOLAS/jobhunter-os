using JobHunter.Application.Matching;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Profile;

namespace JobHunter.UnitTests.Matching;

public class SkillMentionTests
{
    private static string[] Keys(string text) => SkillCatalog.MentionsIn(text).Select(s => s.Key).ToArray();

    [Theory]
    [InlineData("C#/.NET", new[] { "csharp", "dotnet" })]
    [InlineData("Strong in C#, ASP.NET Core and PostgreSQL.", new[] { "csharp", "aspnetcore", "postgresql" })]
    [InlineData("Node.js, Next.js and React Native", new[] { "nodejs", "nextjs", "reactnative" })]
    [InlineData("React and Vue", new[] { "react", "vue" })]
    [InlineData("SQL Server, not MySQL", new[] { "sqlserver", "mysql" })]
    [InlineData("GitHub Actions for CI/CD", new[] { "githubactions", "cicd" })]
    [InlineData("event driven and event-driven", new[] { "eventdriven" })]
    [InlineData("Golang services", new[] { "go" })]
    [InlineData("We build in Go.", new[] { "go" })]
    [InlineData("java, python and postgres", new[] { "java", "python", "postgresql" })]
    [InlineData("k8s on AWS", new[] { "kubernetes", "aws" })]
    [InlineData("Python-based services", new[] { "python" })]
    [InlineData("C++ required", new[] { "cplusplus" })]
    [InlineData("REST APIs", new[] { "rest" })]
    public void Names_are_found_in_order_each_once(string text, string[] keys) => Assert.Equal(keys, Keys(text));

    [Theory]
    [InlineData("Go to market with the team")]
    [InlineData("You will go far and react quickly")]
    [InlineData("Take a rest in spring")]
    [InlineData("Next, we swiftly express ideas")]
    [InlineData("Series C funding and C-level access")]
    [InlineData("Reactive thinking and Javanese food")]
    [InlineData("Non-Java teams")]
    [InlineData("Live in Maui")]
    public void Everyday_words_are_not_skills(string text) => Assert.Empty(Keys(text));
}

/// <summary>Drafts only: the user confirms them. These pin what gets drafted, in the shapes real postings have.</summary>
public class RuleBasedRequirementExtractorTests
{
    private static readonly ISkillCatalog Catalog = new TestCatalog();

    private static IReadOnlyList<ExtractedRequirement> Read(string posting) => new RuleBasedRequirementExtractor(Catalog).Extract(posting);

    private static ExtractedRequirement Row(IEnumerable<ExtractedRequirement> rows, string key) => rows.Single(r => r.SkillKey == key);

    [Fact]
    public void A_requirements_section_is_must_and_a_nice_section_is_nice()
    {
        var rows = Read("""
            About the role
            You'll work with Kubernetes every day.

            Requirements
            - Strong C# skills
            - PostgreSQL

            Nice to have
            - GraphQL

            Benefits
            - A Docker-themed mug
            """);

        Assert.Equal(["csharp", "postgresql", "graphql"], rows.Select(r => r.SkillKey));
        Assert.Equal("must", Row(rows, "csharp").Kind);
        Assert.Equal("Strong C# skills", Row(rows, "csharp").Text);
        Assert.Equal("nice", Row(rows, "graphql").Kind);
    }

    [Fact]
    public void Spanish_sections_read_the_same()
    {
        var rows = Read("""
            Responsabilidades
            - Mantener servicios en Kubernetes

            Requisitos
            - 3 años de experiencia con Python

            Deseable
            - Conocimientos de Docker

            Beneficios
            - Clases de Terraform
            """);

        Assert.Equal(["python", "docker"], rows.Select(r => r.SkillKey));
        Assert.Equal(("must", 3), (Row(rows, "python").Kind, Row(rows, "python").YearsMin));
        Assert.Equal("nice", Row(rows, "docker").Kind);
    }

    [Theory]
    [InlineData("5+ years of Python", 5)]
    [InlineData("5 + years of Python", 5)]
    [InlineData("At least 3 years of Python", 3)]
    [InlineData("Minimum 4 yrs of Python", 4)]
    [InlineData("3-5 years of Python", 3)]
    [InlineData("3 – 5 years of Python", 3)]
    [InlineData("2 or more years of Python", 2)]
    [InlineData("Mínimo 6 años con Python", 6)]
    [InlineData("4 o más años con Python", 4)]
    [InlineData("Python", null)]
    public void Years_come_from_what_the_line_states(string line, int? years)
    {
        var rows = Read($"Requirements\n- {line}");

        Assert.Equal(years, Row(rows, "python").YearsMin);
    }

    [Fact]
    public void Numbers_that_cannot_be_years_are_not_years()
    {
        var rows = Read("Requirements\n- Python, 99 years of it");

        Assert.Null(Row(rows, "python").YearsMin);
    }

    [Fact]
    public void A_line_naming_several_skills_gets_a_row_each_named_after_the_skill()
    {
        var rows = Read("Requirements\n- 5+ years with C#, .NET and PostgreSQL");

        Assert.Equal(["csharp", "dotnet", "postgresql"], rows.Select(r => r.SkillKey));
        Assert.All(rows, r => Assert.Equal(5, r.YearsMin));
        Assert.Equal("C#, 5+ years", Row(rows, "csharp").Text);
    }

    [Theory]
    [InlineData("Experience with Go or Python")]
    [InlineData("Experiencia con Go o Python")]
    [InlineData("Go/Python backend experience")]
    public void Alternatives_are_nice_because_either_one_will_do(string line)
    {
        var rows = Read($"Requirements\n- {line}");

        Assert.Equal(["go", "python"], rows.Select(r => r.SkillKey));
        Assert.All(rows, r => Assert.Equal("nice", r.Kind));
    }

    [Fact]
    public void An_inline_signal_beats_the_section()
    {
        var rows = Read("""
            Requirements
            - GraphQL is a plus
            - Docker preferred

            Nice to have
            - Terraform is required
            """);

        Assert.Equal("nice", Row(rows, "graphql").Kind);
        Assert.Equal("nice", Row(rows, "docker").Kind);
        Assert.Equal("must", Row(rows, "terraform").Kind);
    }

    [Fact]
    public void Sentences_are_read_separately_without_breaking_dotted_names()
    {
        var rows = Read("We need Node.js. GraphQL is a plus; 2+ years of Docker.");

        Assert.Equal(("nice", "graphql"), (Row(rows, "graphql").Kind, Row(rows, "graphql").SkillKey));
        Assert.Equal(("must", 2), (Row(rows, "docker").Kind, Row(rows, "docker").YearsMin));
        Assert.DoesNotContain(rows, r => r.SkillKey == "nodejs" && r.Kind == "must");
    }

    [Fact]
    public void A_requirement_that_names_no_skill_is_kept_to_be_listed_not_scored()
    {
        var rows = Read("Requirements\n- A degree in computer science\n- 7+ years of professional experience\n- OK");

        Assert.Equal(["A degree in computer science", "7+ years of professional experience"], rows.Select(r => r.Text));
        Assert.All(rows, r => Assert.Null(r.SkillKey));
        Assert.Equal(7, rows[1].YearsMin);
    }

    [Fact]
    public void An_unknown_heading_ends_the_section_before_it()
    {
        var rows = Read("""
            Requirements
            - Python

            The Engineering Team
            Our team uses Kubernetes daily, and it is lovely

            Our stack:
            We also run Terraform
            """);

        Assert.Equal(["python"], rows.Select(r => r.SkillKey));
    }

    [Fact]
    public void The_same_skill_twice_merges_into_the_stronger_reading_at_its_first_place()
    {
        var rows = Read("""
            Requirements
            - Python
            - Docker

            Nice to have
            - 3+ years of Docker
            - GraphQL
            - Python is a plus
            """);

        Assert.Equal(["python", "docker", "graphql"], rows.Select(r => r.SkillKey));
        Assert.Equal("must", Row(rows, "python").Kind);
        Assert.Equal(("must", 3), (Row(rows, "docker").Kind, Row(rows, "docker").YearsMin));
    }

    [Fact]
    public void With_no_structure_at_all_the_named_skills_are_drafted_as_nice()
    {
        var rows = Read("We ship C# services on Azure and care a lot about our customers.");

        Assert.Equal(["csharp", "azure"], rows.Select(r => r.SkillKey));
        Assert.All(rows, r => Assert.Equal("nice", r.Kind));
        Assert.Equal("C#", Row(rows, "csharp").Text);
    }

    // The shapes below come from real postings on Greenhouse, Lever and Ashby (read 2026-11-02).

    [Fact]
    public void Real_shape_minimum_and_preferred_with_prose_between_them()
    {
        var rows = Read("""
            Who We Are
            Stripe is a financial infrastructure platform that happens to run a lot of Kubernetes for businesses.

            Responsibilities
            - Debug, extend, and maintain backend systems in Terraform

            Minimum Requirements
            - 4+ years of experience as an engineer shipping production systems
            - Proficiency in at least two of: Ruby, Node.js, Python, or Next.js
            - Familiarity with cloud infrastructure (AWS, GCP) including deployment

            Preferred Qualifications
            - Familiarity with the payments domain

            Who You Are
            Beyond the technical requirements, we're looking for a specific kind of person:
            - You're a pragmatic builder. You ship working solutions quickly, and keep going.
            """);

        Assert.Equal(
            ["4+ years of experience as an engineer shipping production systems", "Ruby", "Node.js", "Python", "Next.js", "Amazon Web Services", "Google Cloud", "Familiarity with the payments domain"],
            rows.Select(r => r.Text));
        Assert.Equal(("must", 4), (rows[0].Kind, rows[0].YearsMin));
        Assert.Equal("nice", Row(rows, "ruby").Kind); // "at least two of" is alternatives
        Assert.Equal("must", Row(rows, "aws").Kind);
        Assert.Equal("nice", rows[^1].Kind);
    }

    [Fact]
    public void Real_shape_a_paragraph_and_a_salary_heading_end_the_list()
    {
        var rows = Read("""
            What You'll Be Doing
            - Own the lifecycle of models in Python

            What you should have
            - You have 5+ years of experience as a Machine Learning Engineer
            - You have strong communication skills and the ability to work with others

            The base salary range for this full-time position is listed below and we use Kubernetes to pay it.

            Base Salary
            $272,000—$340,000 USD

            Why Discord?
            Discord is committed to inclusion and providing reasonable accommodations for Docker users.
            """);

        Assert.Equal(["machinelearning", "communication"], rows.Select(r => r.SkillKey));
        Assert.Equal(5, Row(rows, "machinelearning").YearsMin);
    }

    [Fact]
    public void Real_shape_a_section_about_who_should_not_apply_is_not_a_requirement()
    {
        var rows = Read("""
            WHY YOU SHOULD OR SHOULDN'T APPLY
            - You love being technical and can hold in-depth conversations

            Put another way, you shouldn't apply if:
            - You don't enjoy coding or don't find time to stay up-to-date with Rust

            ENGINEERING CULTURE
            - Minimal process and lots of TypeScript

            Requirements
            - PostgreSQL
            """);

        Assert.Equal(["postgresql"], rows.Select(r => r.SkillKey));
    }

    [Fact]
    public void Real_shape_equal_opportunity_boilerplate_in_a_bullet_ends_the_list()
    {
        var rows = Read("""
            Requirements
            - PostgreSQL
            - Lever is an Equal Opportunity Employer
            - We use Kubernetes to support applicants
            """);

        Assert.Equal(["postgresql"], rows.Select(r => r.SkillKey));
    }

    [Fact]
    public void A_bullet_with_several_sentences_and_no_skill_is_one_requirement_named_by_its_first_sentence()
    {
        var rows = Read("Requirements\n- You thrive without a traditional team structure. No PRDs landing in your lap. You ship it.");

        Assert.Equal("You thrive without a traditional team structure.", Assert.Single(rows).Text);
    }

    [Fact]
    public void Abbreviations_dont_end_a_sentence()
    {
        var rows = Read("Requirements\n- B.S. in Computer Science or equivalent experience\n- Tools e.g. Python");

        Assert.Equal("B.S. in Computer Science or equivalent experience", rows[0].Text);
        Assert.Equal("Tools e.g. Python", Row(rows, "python").Text);
    }

    [Fact]
    public void Prose_in_a_section_counts_only_when_it_names_a_skill()
    {
        var rows = Read("""
            Requirements
            Strong C# skills
            5+ years of .NET
            A person who is kind
            """);

        Assert.Equal(["csharp", "dotnet"], rows.Select(r => r.SkillKey));
    }

    [Theory]
    [InlineData("Required: C#, Docker")]
    [InlineData("Skills: C#, Docker")]
    [InlineData("Requirements: C#, Docker")]
    public void A_label_with_skills_on_the_same_line_is_a_requirement_not_a_heading(string line)
    {
        var rows = Read($"We build things here.\n{line}");

        Assert.Equal(["csharp", "docker"], rows.Select(r => r.SkillKey));
        Assert.All(rows, r => Assert.Equal("must", r.Kind));
    }

    [Fact]
    public void Soft_skills_mentioned_in_passing_are_not_drafted()
    {
        var rows = Read("Our leadership values communication, and we ship Rust.");

        Assert.Equal(["rust"], rows.Select(r => r.SkillKey));
    }

    [Fact]
    public void A_nested_list_marker_is_stripped()
    {
        Assert.Equal("Own the roadmap with care", Assert.Single(Read("Requirements\n- - Own the roadmap with care")).Text);
    }

    [Fact]
    public void Nothing_to_read_gives_nothing()
    {
        Assert.Empty(Read("We care a lot about our customers."));
        Assert.Empty(Read(""));
    }

    [Fact]
    public void Hostile_lengths_are_skipped_and_rows_are_capped()
    {
        Assert.Empty(Read("Requirements\n- " + new string('x', 5000)));

        var many = "Requirements\n" + string.Join('\n', Enumerable.Range(0, 200).Select(i => $"- Requirement number {i} of the many"));
        Assert.Equal(50, Read(many).Count);
    }

    [Fact]
    public void A_long_requirement_is_cut_to_the_stored_limit()
    {
        var rows = Read("Requirements\n- " + new string('y', 400) + " detail");

        Assert.Equal(300, Assert.Single(rows).Text.Length);
    }

    private sealed class TestCatalog : ISkillCatalog
    {
        public string KeyFor(string name) => SkillKey.For(name);
        public CatalogSkill? Find(string key) => SkillCatalog.FindByKey(key) is { } s ? new(s.Key, s.Name, s.Category, s.Family) : null;
        public IReadOnlyList<CatalogSkill> Search(string query, int limit) => [];
        public IReadOnlyList<CatalogSkill> MentionsIn(string text) =>
            SkillCatalog.MentionsIn(text).Select(s => new CatalogSkill(s.Key, s.Name, s.Category, s.Family)).ToList();
    }
}
