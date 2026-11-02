using System.Net;
using System.Text;
using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Infrastructure.Opportunities.JobBoards;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobHunter.UnitTests.Opportunities;

/// <summary>Fixtures keep the shape of the providers' real responses (checked against them on 2026-11-01).</summary>
public class JobBoardClientsTests
{
    private const string GreenhouseJson = """
        {"jobs":[{"absolute_url":"https://job-boards.greenhouse.io/discord/jobs/8840756002","data_compliance":[],"internal_job_id":6539280002,
        "location":{"name":"San Francisco Bay Area"},"metadata":null,"id":8840756002,"updated_at":"2026-10-20T20:14:41-04:00","requisition_id":"1",
        "title":"Data Scientist - Client Platform","company_name":"Discord","first_published":"2026-01-01T00:00:00-04:00","language":"en",
        "content":"&lt;div class=&quot;content-intro&quot;&gt;&lt;p&gt;Discord has millions of users.&lt;/p&gt;&lt;/div&gt;&lt;ul&gt;&lt;li&gt;SQL &amp;amp; Python&lt;/li&gt;&lt;/ul&gt;&lt;p&gt;The US base salary range for this full-time position is $196,000 to $220,500.&lt;/p&gt;",
        "departments":[],"offices":[]},
        {"id":2,"title":"Remote Engineer","company_name":"Discord","location":{"name":"Remote - Canada"},"absolute_url":"https://job-boards.greenhouse.io/discord/jobs/2",
        "content":"&lt;p&gt;Salary: $100,000 per year.&lt;/p&gt;"}],"meta":{"total":2}}
        """;

    private const string LeverJson = """
        [{"additional":"","additionalPlain":"We offer learning budgets.","categories":{"commitment":"Regular Full Time (Salary)","department":"Customer Success","location":"Atlanta, Georgia","team":"CS","allLocations":["Atlanta, Georgia"]},
        "createdAt":1788199939703,"descriptionPlain":"Join a scaling team.","description":"<div>Join a scaling team.</div>","id":"ad208490-4052-4f91-a57e-433f2d1e484b",
        "lists":[{"text":"Skill Set:","content":"<li>3+ years of account management</li><li>CRM tools</li>"}],
        "salaryRange":{"min":85000,"max":175000,"currency":"USD","interval":"per-year-salary"},"text":"Customer Success Manager","country":"US","workplaceType":"hybrid",
        "hostedUrl":"https://jobs.lever.co/leverdemo/ad208490-4052-4f91-a57e-433f2d1e484b","applyUrl":"https://jobs.lever.co/leverdemo/ad208490-4052-4f91-a57e-433f2d1e484b/apply"}]
        """;

    private const string AshbyJson = """
        {"apiVersion":"1","jobs":[{"id":"7458d4e9-da2e-47bd-98cb-adfda43d42b2","title":"Engineering Manager - EU","department":"Engineering","team":"EMEA","employmentType":"FullTime",
        "location":"Remote - European Union","shouldDisplayCompensationOnJobPostings":true,"secondaryLocations":[],"publishedAt":"2026-01-01T00:00:00Z","isListed":true,"isRemote":true,"workplaceType":"Remote",
        "address":null,"jobUrl":"https://jobs.ashbyhq.com/ashby/7458d4e9-da2e-47bd-98cb-adfda43d42b2","applyUrl":"https://jobs.ashbyhq.com/ashby/7458d4e9-da2e-47bd-98cb-adfda43d42b2/application",
        "descriptionHtml":"<p>Lead a team.</p>","descriptionPlain":"Lead a team.",
        "compensation":{"summaryComponents":[{"compensationType":"Bonus","interval":"1 YEAR","currencyCode":"EUR","minValue":null,"maxValue":null},{"compensationType":"Salary","interval":"1 YEAR","currencyCode":"EUR","minValue":150000,"maxValue":250000}]}},
        {"id":"hidden","title":"Unlisted role","isListed":false,"descriptionPlain":"Shh."}]}
        """;

    private sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? Requested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Throwing(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static readonly IOptions<ImportOptions> Settings = Options.Create(new ImportOptions());

    private static GreenhouseClient Greenhouse(HttpMessageHandler h) => new(new Factory(h), Settings, NullLogger<GreenhouseClient>.Instance);
    private static LeverClient Lever(HttpMessageHandler h) => new(new Factory(h), Settings, NullLogger<LeverClient>.Instance);
    private static AshbyClient Ashby(HttpMessageHandler h) => new(new Factory(h), Settings, NullLogger<AshbyClient>.Instance);

    [Fact]
    public async Task Greenhouse_postings_decode_their_escaped_html()
    {
        var stub = new Stub(HttpStatusCode.OK, GreenhouseJson);

        var postings = await Greenhouse(stub).ListAsync("discord", CancellationToken.None);

        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/discord/jobs?content=true", stub.Requested!.ToString());
        var job = postings![0];
        Assert.Equal(("8840756002", "Data Scientist - Client Platform", "Discord", "San Francisco Bay Area"), (job.Id, job.Title, job.CompanyName, job.Location));
        Assert.Equal("https://job-boards.greenhouse.io/discord/jobs/8840756002", job.Url);
        Assert.Contains("- SQL & Python", job.Description);
        Assert.DoesNotContain("<", job.Description);
        Assert.Equal("full_time", job.EmploymentType);
        Assert.Equal((196000, 220500, "USD"), (job.SalaryMin!.Value, job.SalaryMax!.Value, job.SalaryCurrency));

        // Remote stated in its location; a bare "$" with no US mention isn't saved as USD without a preview.
        var remote = postings[1];
        Assert.Equal("remote", remote.WorkMode);
        Assert.Null(remote.SalaryMin);
        Assert.Contains("$100,000", remote.Description);
    }

    [Fact]
    public async Task Lever_postings_include_the_lists_with_the_requirements()
    {
        var stub = new Stub(HttpStatusCode.OK, LeverJson);

        var job = (await Lever(stub).ListAsync("leverdemo", CancellationToken.None))!.Single();

        Assert.Equal("https://api.lever.co/v0/postings/leverdemo?mode=json", stub.Requested!.ToString());
        Assert.Equal(("ad208490-4052-4f91-a57e-433f2d1e484b", "Customer Success Manager", "Atlanta, Georgia"), (job.Id, job.Title, job.Location));
        Assert.Null(job.CompanyName);
        Assert.Equal(("hybrid", "full_time"), (job.WorkMode, job.EmploymentType));
        Assert.Equal((85000, 175000, "USD", "year"), (job.SalaryMin!.Value, job.SalaryMax!.Value, job.SalaryCurrency, job.SalaryPeriod));
        Assert.Equal("Join a scaling team.\n\nSkill Set:\n- 3+ years of account management\n- CRM tools\n\nWe offer learning budgets.", job.Description);
    }

    [Fact]
    public async Task Ashby_postings_read_compensation_and_leave_out_unlisted_ones()
    {
        var stub = new Stub(HttpStatusCode.OK, AshbyJson);

        var postings = await Ashby(stub).ListAsync("ashby", CancellationToken.None);

        Assert.Equal("https://api.ashbyhq.com/posting-api/job-board/ashby?includeCompensation=true", stub.Requested!.ToString());
        var job = Assert.Single(postings!);
        Assert.Equal(("remote", "full_time"), (job.WorkMode, job.EmploymentType));
        Assert.Equal((150000, 250000, "EUR", "year"), (job.SalaryMin!.Value, job.SalaryMax!.Value, job.SalaryCurrency, job.SalaryPeriod));
        Assert.Equal("Lead a team.", job.Description);
    }

    private sealed class FlakyThenOk(HttpStatusCode first, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(++Calls == 1
                ? new HttpResponseMessage(first)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_gateway_hiccup_is_retried_once(HttpStatusCode first)
    {
        var flaky = new FlakyThenOk(first, LeverJson);

        var postings = await Lever(flaky).ListAsync("leverdemo", CancellationToken.None);

        Assert.Single(postings!);
        Assert.Equal(2, flaky.Calls);
    }

    [Fact]
    public async Task Other_errors_are_not_retried()
    {
        var flaky = new FlakyThenOk(HttpStatusCode.InternalServerError, LeverJson);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => Lever(flaky).ListAsync("leverdemo", CancellationToken.None));

        Assert.Equal(1, flaky.Calls);
    }

    [Fact]
    public async Task A_link_that_isnt_a_web_address_is_dropped()
    {
        const string json = """{"jobs":[{"id":1,"title":"T","absolute_url":"javascript:alert(1)","content":""}]}""";

        var job = (await Greenhouse(new Stub(HttpStatusCode.OK, json)).ListAsync("x", CancellationToken.None))!.Single();

        Assert.Null(job.Url);
    }

    [Theory]
    [InlineData(100, 200, "USD", true)]
    [InlineData(null, 200, "USD", true)]
    [InlineData(100, 200, null, false)]      // no currency
    [InlineData(100, 200, "Dollars", false)] // not a code
    [InlineData(200, 100, "USD", false)]     // backwards
    [InlineData(null, null, "USD", false)]   // no amount
    public void Only_a_usable_salary_survives(int? min, int? max, string? currency, bool kept)
    {
        var posting = new BoardPosting("1", "T", null, null, null, null, null, min, max, currency, "year", "Text");

        var result = posting.WithUsableSalary();

        Assert.Equal(kept, result.SalaryCurrency is not null);
        Assert.Equal(kept ? min : null, result.SalaryMin);
        Assert.Equal(kept ? max : null, result.SalaryMax);
    }

    public static TheoryData<Func<HttpMessageHandler, IJobBoardClient>> AllClients() =>
    [
        h => Greenhouse(h),
        h => Lever(h),
        h => Ashby(h),
    ];

    [Theory]
    [MemberData(nameof(AllClients))]
    public async Task A_missing_board_is_null(Func<HttpMessageHandler, IJobBoardClient> client)
    {
        Assert.Null(await client(new Stub(HttpStatusCode.NotFound, """{"ok":false,"error":"Document not found"}""")).ListAsync("nope", CancellationToken.None));
    }

    [Theory]
    [MemberData(nameof(AllClients))]
    public async Task A_token_that_could_change_the_path_never_reaches_the_network(Func<HttpMessageHandler, IJobBoardClient> client)
    {
        var stub = new Stub(HttpStatusCode.OK, "{}");

        Assert.Null(await client(stub).ListAsync("../admin", CancellationToken.None));
        Assert.Null(stub.Requested);
    }

    [Theory]
    [MemberData(nameof(AllClients))]
    public async Task Provider_failures_are_unavailable(Func<HttpMessageHandler, IJobBoardClient> client)
    {
        foreach (var handler in new HttpMessageHandler[]
        {
            new Stub(HttpStatusCode.InternalServerError, "oops"),
            new Stub(HttpStatusCode.OK, "<html>not json</html>"),
            new Throwing(new HttpRequestException("network down")),
            new Throwing(new TaskCanceledException("timed out")),
        })
        {
            var failure = await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client(handler).ListAsync("contoso", CancellationToken.None));
            Assert.Equal("source.unavailable", failure.Code);
        }
    }
}
