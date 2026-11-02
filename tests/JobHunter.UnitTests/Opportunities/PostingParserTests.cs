using JobHunter.Application.Opportunities.Imports;
using JobHunter.Domain.Opportunities;
using JobHunter.Infrastructure.Opportunities.Imports;

namespace JobHunter.UnitTests.Opportunities;

public class PostingParserTests
{
    private readonly PostingParser _parser = new();

    private const string JsonLdPage = """
        <html><head><title>Ignored title | Careers</title>
        <script type="application/ld+json">{ "@context": "https://schema.org", "@type": "WebSite", "name": "Not a job" }</script>
        <script type="application/ld+json">
        { "@context": "https://schema.org", "@type": "JobPosting",
          "title": "Senior Backend Engineer &amp; Mentor",
          "hiringOrganization": { "@type": "Organization", "name": "Contoso" },
          "jobLocation": { "@type": "Place", "address": { "addressLocality": "Medellín", "addressRegion": "Antioquia", "addressCountry": { "name": "Colombia" } } },
          "employmentType": ["FULL_TIME"],
          "baseSalary": { "@type": "MonetaryAmount", "currency": "usd", "value": { "@type": "QuantitativeValue", "minValue": 4000, "maxValue": "5500.00", "unitText": "MONTH" } },
          "description": "&lt;p&gt;Build APIs.&lt;/p&gt;&lt;ul&gt;&lt;li&gt;&lt;p&gt;5+ years of C#&lt;/p&gt;&lt;/li&gt;&lt;li&gt;PostgreSQL&lt;/li&gt;&lt;/ul&gt;&lt;script&gt;alert(1)&lt;/script&gt;" }
        </script></head><body><p>Page chrome</p></body></html>
        """;

    [Fact]
    public void Structured_data_wins_and_is_read_field_by_field()
    {
        var p = _parser.ParseHtml(JsonLdPage);

        Assert.Equal(ParsedFrom.StructuredData, p.ParsedFrom);
        Assert.Equal("Senior Backend Engineer & Mentor", p.Title);
        Assert.Equal("Contoso", p.CompanyName);
        Assert.Equal("Medellín, Antioquia, Colombia", p.Location);
        Assert.Equal("full_time", p.EmploymentType);
        Assert.Equal((4000, 5500, "USD", "month"), (p.SalaryMin!.Value, p.SalaryMax!.Value, p.SalaryCurrency, p.SalaryPeriod));
        Assert.Equal("Build APIs.\n\n- 5+ years of C#\n- PostgreSQL", p.Description);
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void A_posting_inside_a_graph_with_a_remote_flag_is_found()
    {
        const string html = """
            <script type="application/ld+json">{"@graph":[{"@type":"Organization","name":"x"},
            {"@type":["JobPosting"],"title":"SRE","hiringOrganization":"Fabrikam","jobLocationType":"TELECOMMUTE","description":"Keep it running."}]}</script>
            """;

        var p = _parser.ParseHtml(html);

        Assert.Equal(("SRE", "Fabrikam", "Remote", "remote"), (p.Title, p.CompanyName, p.Location, p.WorkMode));
    }

    [Fact]
    public void Broken_structured_data_falls_back_to_the_page()
    {
        var text = string.Join(' ', Enumerable.Repeat("We build reliable systems for small teams.", 10));
        var html = $$"""
            <html><head><title>Backend Engineer at Northwind | Careers</title>
            <script type="application/ld+json">{ this is not json </script></head>
            <body><nav>Home Jobs</nav><main><h1>Backend Engineer</h1><p>{{text}}</p></main><footer>© Northwind</footer>
            <script>track()</script></body></html>
            """;

        var p = _parser.ParseHtml(html);

        Assert.Equal(ParsedFrom.PageText, p.ParsedFrom);
        Assert.Equal("Backend Engineer", p.Title);
        Assert.Equal("Northwind", p.CompanyName);
        Assert.DoesNotContain("Home Jobs", p.Description);
        Assert.DoesNotContain("track()", p.Description);
        Assert.DoesNotContain("©", p.Description);
        Assert.Empty(p.Warnings);
    }

    [Fact]
    public void A_page_with_almost_no_text_warns_to_paste_instead()
    {
        var p = _parser.ParseHtml("<html><head><title>Loading…</title></head><body><div id=\"root\"></div></body></html>");

        Assert.Contains(p.Warnings, w => w.Contains("paste the posting"));
    }

    [Theory]
    [InlineData("Backend Engineer - Contoso | Careers", "Backend Engineer", "Contoso")]
    [InlineData("Jobs | Data Engineer | Fabrikam", "Data Engineer", "Fabrikam")]
    [InlineData("Backend Engineer at Tailspin Toys", "Backend Engineer", "Tailspin Toys")]
    [InlineData("Desarrollador .NET en Bancolombia - Empleos", "Desarrollador .NET", "Bancolombia")]
    [InlineData("Platform Engineer", "Platform Engineer", null)]
    public void Page_titles_split_into_role_and_company(string pageTitle, string title, string? company)
    {
        var p = _parser.ParseHtml($"<html><head><title>{pageTitle}</title></head><body></body></html>");

        Assert.Equal(title, p.Title);
        Assert.Equal(company, p.CompanyName);
    }

    [Fact]
    public void Pasted_text_reads_labels_first()
    {
        const string text = """
            We are hiring!
            Position: Backend Engineer
            Company: Contoso
            Location: Bogotá, Colombia
            Remote, full-time.

            Compensation: USD 4,000 - 5,500 per month.
            You will build APIs.
            """;

        var p = _parser.ParseText(text);

        Assert.Equal(ParsedFrom.PastedText, p.ParsedFrom);
        Assert.Equal(("Backend Engineer", "Contoso", "Bogotá, Colombia"), (p.Title, p.CompanyName, p.Location));
        Assert.Equal(("remote", "full_time"), (p.WorkMode, p.EmploymentType));
        Assert.Equal((4000, 5500, "USD", "month"), (p.SalaryMin!.Value, p.SalaryMax!.Value, p.SalaryCurrency, p.SalaryPeriod));
        Assert.Contains("You will build APIs.", p.Description);
    }

    [Fact]
    public void Pasted_text_without_labels_uses_the_first_line_and_guesses_nothing_else()
    {
        var p = _parser.ParseText("Data Engineer at Fabrikam\n\nWe move data. Teams are remote or hybrid; some work on-site.");

        Assert.Equal(("Data Engineer", "Fabrikam"), (p.Title, p.CompanyName));
        Assert.Null(p.Location);
        Assert.Null(p.WorkMode); // three modes mentioned: ambiguous, so nothing
        Assert.Null(p.SalaryMin);
    }

    [Fact]
    public void A_first_line_that_is_a_sentence_is_not_a_title()
    {
        Assert.Null(_parser.ParseText("We are a small company that builds tools for people who care about their craft.\nMore.").Title);
    }

    [Fact]
    public void Long_values_are_cut_to_the_opportunity_limits()
    {
        var p = _parser.ParseText($"Title: {new string('x', 300)}\nCompany: {new string('y', 300)}\n{new string('z', 60_000)}");

        Assert.Equal(160, p.Title!.Length);
        Assert.Equal(120, p.CompanyName!.Length);
        Assert.Equal(50_000, p.Description.Length);
    }

    [Theory]
    // Stated pay, with the forms postings really use
    [InlineData("Salary: $120,000 - $150,000", 120000, 150000, "USD", null, true)]
    [InlineData("The US base salary range for this position is $85,000 - $135,000", 85000, 135000, "USD", null, false)]
    [InlineData("$60/hour", 60, 60, "USD", "hour", true)]
    [InlineData("€60k–€70k per year", 60000, 70000, "EUR", "year", false)]
    [InlineData("Compensation: 4.000 a 5.500 USD al mes", 4000, 5500, "USD", "month", false)]
    [InlineData("Salario: COP 8.000.000 mensual", 8000000, 8000000, "COP", "month", false)]
    [InlineData("£55,000 a year", 55000, 55000, "GBP", "year", false)]
    public void Salaries_are_read_when_stated(string line, int min, int max, string currency, string? period, bool assumed)
    {
        var salary = TextFacts.Salary(line);

        Assert.NotNull(salary);
        Assert.Equal((min, max, currency, period, assumed), (salary.Min!.Value, salary.Max!.Value, salary.Currency, salary.Period, salary.CurrencyAssumed));
    }

    [Theory]
    // Shape from a real Greenhouse posting (Discord, 2026-11-01): a heading, then the amount.
    [InlineData("Base Salary\n\n$160,000—$200,000 USD", 160000, 200000, "USD", false)]
    [InlineData("Compensation\n$90,000 - $110,000 CAD", 90000, 110000, "CAD", false)]
    [InlineData("Pay range\n$100,000 - $120,000", 100000, 120000, "USD", true)]
    public void A_short_pay_heading_counts_for_the_line_under_it(string text, int min, int max, string currency, bool assumed)
    {
        var salary = TextFacts.Salary(text);

        Assert.NotNull(salary);
        Assert.Equal((min, max, currency, assumed), (salary.Min!.Value, salary.Max!.Value, salary.Currency, salary.CurrencyAssumed));
    }

    [Theory]
    [InlineData("Our salary ranges are determined by role, level, location and a long list of other factors that we review.\n$160,000 raised")]
    [InlineData("Base Salary\nWe are growing fast\n$50,000 grant")] // the heading isn't right above the amount
    public void A_sentence_or_a_distant_heading_doesnt_make_a_number_pay(string text)
    {
        Assert.Null(TextFacts.Salary(text));
    }

    [Theory]
    [InlineData("We raised $40M from great investors")]
    [InlineData("Compensation benchmarked against $1B revenue companies")]
    [InlineData("Serving 10,000 customers across 20 countries")]
    [InlineData("Salary: competitive")]
    [InlineData("5+ years of experience")]
    [InlineData("Salary: 120000")] // no currency
    [InlineData("We spent $50,000 on the team offsite")] // no pay word, no period
    public void Other_money_and_numbers_are_not_salaries(string line)
    {
        Assert.Null(TextFacts.Salary(line));
    }

    [Fact]
    public void A_bare_dollar_is_flagged_as_an_assumption()
    {
        var p = _parser.ParseText("Engineer\nSalary: $100,000 per year");

        Assert.Equal("USD", p.SalaryCurrency);
        Assert.Contains(p.Warnings, w => w.Contains("USD is assumed"));
    }

    [Fact]
    public void A_huge_hostile_line_is_skipped_quickly()
    {
        var hostile = "Salary " + string.Concat(Enumerable.Repeat("1,", 200_000));

        Assert.Null(TextFacts.Salary(hostile));
    }

    [Theory]
    [InlineData("<p>One</p><p>Two</p>", "One\n\nTwo")]
    [InlineData("<ul><li>A</li><li><p>B</p></li></ul>", "- A\n- B")]
    [InlineData("Line one<br>Line two", "Line one\nLine two")]
    [InlineData("<h2>Title</h2>Body &amp; more", "Title\n\nBody & more")]
    [InlineData("<script>evil()</script><style>x{}</style><p>Kept</p>", "Kept")]
    public void Html_reduces_to_plain_text(string html, string text)
    {
        Assert.Equal(text, HtmlText.From(html));
    }
}

public class ImportRulesTests
{
    [Theory]
    [InlineData("www.linkedin.com", "LinkedIn")]
    [InlineData("linkedin.com", "LinkedIn")]
    [InlineData("co.linkedin.com", "LinkedIn")]
    [InlineData("lnkd.in", "LinkedIn")]
    [InlineData("de.indeed.com", "Indeed")]
    [InlineData("indeed.co.uk", "Indeed")]
    [InlineData("www.glassdoor.de", "Glassdoor")]
    [InlineData("www.ziprecruiter.com", "ZipRecruiter")]
    [InlineData("MONSTER.COM", "Monster")]
    public void Sites_that_forbid_automated_reading_are_recognized(string host, string name)
    {
        Assert.Equal(name, ImportRules.ForbiddenSite(host));
    }

    [Theory]
    [InlineData("careers.monster.energy")]
    [InlineData("indeedflex.com")]
    [InlineData("notlinkedin.com")]
    [InlineData("jobs.contoso.com")]
    public void Lookalikes_are_not(string host)
    {
        Assert.Null(ImportRules.ForbiddenSite(host));
    }

    [Theory]
    [InlineData("https://boards.greenhouse.io/contoso/jobs/4027", "greenhouse", "contoso", "4027")]
    [InlineData("https://job-boards.greenhouse.io/discord/jobs/8840756002?gh_src=x", "greenhouse", "discord", "8840756002")]
    [InlineData("https://jobs.lever.co/leverdemo/ad208490-4052-4f91-a57e-433f2d1e484b", "lever", "leverdemo", "ad208490-4052-4f91-a57e-433f2d1e484b")]
    [InlineData("https://jobs.lever.co/leverdemo/ad208490-4052-4f91-a57e-433f2d1e484b/apply", "lever", "leverdemo", "ad208490-4052-4f91-a57e-433f2d1e484b")]
    [InlineData("https://jobs.ashbyhq.com/ashby/7458d4e9-da2e-47bd-98cb-adfda43d42b2", "ashby", "ashby", "7458d4e9-da2e-47bd-98cb-adfda43d42b2")]
    public void Links_to_one_posting_on_a_job_board_are_recognized(string url, string provider, string board, string id)
    {
        Assert.Equal(new JobBoardLink(provider, board, id), ImportRules.ParseJobBoardLink(new Uri(url)));
    }

    [Theory]
    [InlineData("https://boards.greenhouse.io/contoso")]
    [InlineData("https://boards.greenhouse.io/contoso/jobs/not-a-number")]
    [InlineData("https://jobs.lever.co/leverdemo")]
    [InlineData("https://example.com/contoso/jobs/4027")]
    public void Anything_else_is_not(string url)
    {
        Assert.Null(ImportRules.ParseJobBoardLink(new Uri(url)));
    }

    [Fact]
    public void Tokens_that_could_change_the_api_path_are_invalid()
    {
        Assert.True(ImportRules.IsValidBoardToken("contoso-inc_2"));
        Assert.False(ImportRules.IsValidBoardToken("../admin"));
        Assert.False(ImportRules.IsValidBoardToken("a b"));
        Assert.False(ImportRules.IsValidBoardToken(""));
        Assert.False(ImportRules.IsValidBoardToken(null));
    }

    [Fact]
    public void Board_names_are_tidied_into_labels()
    {
        Assert.Equal("Contoso Inc", ImportRules.TidyBoardName("contoso-inc"));
        Assert.Equal("Leverdemo", ImportRules.TidyBoardName("leverdemo"));
    }

    [Fact]
    public void Url_refs_ignore_fragments_and_campaign_tags_but_not_real_queries()
    {
        var plain = PostingRefs.ForUrl(new Uri("https://Jobs.Example.com/role/"));

        Assert.Equal(plain, PostingRefs.ForUrl(new Uri("https://jobs.example.com/role?utm_source=x&utm_medium=y#apply")));
        Assert.NotEqual(plain, PostingRefs.ForUrl(new Uri("https://jobs.example.com/role?id=7")));
        Assert.Equal(64, plain.Length);
        Assert.Equal("contoso:42", PostingRefs.ForBoard("contoso", "42"));
    }
}
