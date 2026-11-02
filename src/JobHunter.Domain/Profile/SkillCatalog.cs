using System.Text.RegularExpressions;

namespace JobHunter.Domain.Profile;

/// <summary>A skill the catalog knows: its key, display name, category, family and other names.</summary>
/// <param name="Family">Skills that partly transfer to each other (phase 5 scores them as related), or null.</param>
public sealed record KnownSkill(string Key, string Name, string Category, string? Family, params string[] Aliases);

/// <summary>
/// The curated list behind skill keys and autocomplete (spec 03, Profile). Keys are stable:
/// stored skills and requirements refer to them, so rename a display name freely but never
/// change a key. Aliases are matched in compact form (<see cref="SkillKey.Compact"/>).
/// </summary>
public static class SkillCatalog
{
    public static readonly IReadOnlyList<KnownSkill> All =
    [
        // Languages
        new("csharp", "C#", "language", "dotnet", "c sharp", "csharp"),
        new("fsharp", "F#", "language", "dotnet", "f sharp"),
        new("vbnet", "VB.NET", "language", "dotnet", "visual basic"),
        new("java", "Java", "language", "jvm"),
        new("kotlin", "Kotlin", "language", "jvm"),
        new("scala", "Scala", "language", "jvm"),
        new("javascript", "JavaScript", "language", "javascript", "js", "ecmascript", "es6"),
        new("typescript", "TypeScript", "language", "javascript", "ts"),
        new("python", "Python", "language", "python", "py"),
        new("go", "Go", "language", null, "golang"),
        new("rust", "Rust", "language", null),
        new("cplusplus", "C++", "language", "c-family", "cpp"),
        new("c", "C", "language", "c-family"),
        new("php", "PHP", "language", null),
        new("ruby", "Ruby", "language", null),
        new("swift", "Swift", "language", "apple"),
        new("dart", "Dart", "language", null),
        new("sql", "SQL", "language", "sql", "tsql", "t-sql", "plsql", "pl/sql"),
        new("bash", "Bash", "language", "shell", "shell scripting", "sh"),
        new("powershell", "PowerShell", "language", "shell", "pwsh"),
        new("html", "HTML", "language", "web-markup", "html5"),
        new("css", "CSS", "language", "web-markup", "css3", "scss", "sass"),

        // .NET and backend frameworks
        new("dotnet", ".NET", "framework", "dotnet", "dotnet", "net core", "dotnet core", "dotnetcore", "net framework"),
        new("aspnetcore", "ASP.NET Core", "framework", "dotnet", "asp.net", "aspnet", "asp.net mvc", "web api", "aspnet core"),
        new("efcore", "Entity Framework Core", "framework", "dotnet", "ef core", "entity framework", "ef"),
        new("blazor", "Blazor", "framework", "dotnet"),
        new("maui", ".NET MAUI", "framework", "dotnet", "maui", "xamarin"),
        new("signalr", "SignalR", "framework", "dotnet"),
        new("spring", "Spring Boot", "framework", "jvm", "spring", "springboot"),
        new("nodejs", "Node.js", "framework", "javascript", "node", "nodejs"),
        new("express", "Express", "framework", "javascript", "expressjs", "express.js"),
        new("nestjs", "NestJS", "framework", "javascript", "nest"),
        new("django", "Django", "framework", "python"),
        new("fastapi", "FastAPI", "framework", "python"),
        new("flask", "Flask", "framework", "python"),
        new("rails", "Ruby on Rails", "framework", null, "rails", "ror"),
        new("laravel", "Laravel", "framework", null),

        // Frontend and mobile
        new("react", "React", "framework", "frontend", "reactjs", "react.js"),
        new("nextjs", "Next.js", "framework", "frontend", "next", "nextjs"),
        new("angular", "Angular", "framework", "frontend", "angularjs"),
        new("vue", "Vue", "framework", "frontend", "vuejs", "vue.js"),
        new("nuxt", "Nuxt", "framework", "frontend", "nuxtjs"),
        new("svelte", "Svelte", "framework", "frontend", "sveltekit"),
        new("tailwindcss", "Tailwind CSS", "framework", "web-markup", "tailwind"),
        new("reactnative", "React Native", "framework", "mobile", "react native"),
        new("expo", "Expo", "framework", "mobile"),
        new("flutter", "Flutter", "framework", "mobile"),
        new("redux", "Redux", "framework", "frontend"),
        new("tanstackquery", "TanStack Query", "framework", "frontend", "react query"),

        // Data
        new("postgresql", "PostgreSQL", "data", "relational-db", "postgres", "psql"),
        new("sqlserver", "SQL Server", "data", "relational-db", "mssql", "microsoft sql server", "ms sql"),
        new("mysql", "MySQL", "data", "relational-db", "mariadb"),
        new("oracle", "Oracle Database", "data", "relational-db", "oracle", "oracle db"),
        new("sqlite", "SQLite", "data", "relational-db"),
        new("mongodb", "MongoDB", "data", "document-db", "mongo"),
        new("cosmosdb", "Azure Cosmos DB", "data", "document-db", "cosmos db", "cosmos"),
        new("dynamodb", "DynamoDB", "data", "document-db", "dynamo"),
        new("redis", "Redis", "data", "cache"),
        new("elasticsearch", "Elasticsearch", "data", "search", "elastic", "opensearch"),
        new("pgvector", "pgvector", "data", "search", "vector database"),
        new("dapper", "Dapper", "data", "dotnet"),
        new("graphql", "GraphQL", "data", "api-style"),

        // Messaging
        new("rabbitmq", "RabbitMQ", "data", "messaging", "rabbit"),
        new("kafka", "Apache Kafka", "data", "messaging", "kafka"),
        new("azureservicebus", "Azure Service Bus", "data", "messaging", "service bus"),
        new("sqs", "Amazon SQS", "data", "messaging", "aws sqs"),
        new("masstransit", "MassTransit", "framework", "messaging"),
        new("hangfire", "Hangfire", "framework", "dotnet"),

        // Cloud and infrastructure
        new("azure", "Microsoft Azure", "cloud", "cloud", "azure", "microsoft azure"),
        new("aws", "Amazon Web Services", "cloud", "cloud", "aws", "amazon web services"),
        new("gcp", "Google Cloud", "cloud", "cloud", "google cloud platform", "gcp"),
        new("cloudflare", "Cloudflare", "cloud", "cloud", "cloudflare workers"),
        new("docker", "Docker", "cloud", "containers", "containers", "docker compose"),
        new("kubernetes", "Kubernetes", "cloud", "containers", "k8s", "aks", "eks", "gke"),
        new("helm", "Helm", "cloud", "containers"),
        new("terraform", "Terraform", "cloud", "iac", "opentofu"),
        new("bicep", "Bicep", "cloud", "iac", "arm templates"),
        new("cloudformation", "AWS CloudFormation", "cloud", "iac", "cloudformation", "aws cdk", "cdk"),
        new("pulumi", "Pulumi", "cloud", "iac"),
        new("serverless", "Serverless", "cloud", "serverless"),
        new("awslambda", "AWS Lambda", "cloud", "serverless", "lambda"),
        new("azurefunctions", "Azure Functions", "cloud", "serverless"),
        new("linux", "Linux", "tooling", null, "unix"),
        new("nginx", "Nginx", "tooling", null),

        // Delivery and tooling
        new("git", "Git", "tooling", "vcs"),
        new("github", "GitHub", "tooling", "vcs"),
        new("gitlab", "GitLab", "tooling", "vcs"),
        new("githubactions", "GitHub Actions", "tooling", "ci", "gh actions"),
        new("azuredevops", "Azure DevOps", "tooling", "ci", "azure pipelines", "vsts"),
        new("gitlabci", "GitLab CI", "tooling", "ci"),
        new("jenkins", "Jenkins", "tooling", "ci"),
        new("cicd", "CI/CD", "practice", "ci", "continuous integration", "continuous delivery", "continuous deployment"),
        new("opentelemetry", "OpenTelemetry", "tooling", "observability", "otel"),
        new("prometheus", "Prometheus", "tooling", "observability"),
        new("grafana", "Grafana", "tooling", "observability"),
        new("serilog", "Serilog", "tooling", "observability"),
        new("datadog", "Datadog", "tooling", "observability"),
        new("jira", "Jira", "tooling", null),
        new("figma", "Figma", "tooling", "design"),

        // Testing
        new("xunit", "xUnit", "tooling", "dotnet-testing", "xunit.net"),
        new("nunit", "NUnit", "tooling", "dotnet-testing"),
        new("mstest", "MSTest", "tooling", "dotnet-testing"),
        new("jest", "Jest", "tooling", "js-testing"),
        new("vitest", "Vitest", "tooling", "js-testing"),
        new("playwright", "Playwright", "tooling", "e2e-testing"),
        new("cypress", "Cypress", "tooling", "e2e-testing"),
        new("selenium", "Selenium", "tooling", "e2e-testing"),
        new("testcontainers", "Testcontainers", "tooling", null),

        // Practices and architecture
        new("rest", "REST APIs", "practice", "api-style", "rest", "restful", "rest api", "web apis"),
        new("grpc", "gRPC", "practice", "api-style"),
        new("microservices", "Microservices", "practice", "architecture", "microservice architecture"),
        new("cleanarchitecture", "Clean Architecture", "practice", "architecture", "onion architecture", "hexagonal architecture"),
        new("ddd", "Domain-Driven Design", "practice", "architecture", "domain driven design", "ddd"),
        new("cqrs", "CQRS", "practice", "architecture"),
        new("eventsourcing", "Event Sourcing", "practice", "architecture"),
        new("eventdriven", "Event-Driven Architecture", "practice", "architecture", "event driven", "eda"),
        new("tdd", "Test-Driven Development", "practice", null, "tdd", "test driven development"),
        new("oauth", "OAuth 2.0", "practice", "security", "oauth2", "openid connect", "oidc"),
        new("appsec", "Application Security", "practice", "security", "owasp", "secure coding"),
        new("agile", "Agile", "practice", "agile"),
        new("scrum", "Scrum", "practice", "agile"),
        new("systemdesign", "System Design", "practice", "architecture"),

        // AI
        new("llm", "LLM Integration", "domain", "ai", "large language models", "llms", "genai", "generative ai"),
        new("promptengineering", "Prompt Engineering", "practice", "ai"),
        new("rag", "Retrieval-Augmented Generation", "practice", "ai", "rag"),
        new("machinelearning", "Machine Learning", "domain", "ai", "ml"),

        // Soft
        new("communication", "Communication", "soft", null),
        new("leadership", "Leadership", "soft", null, "team lead", "mentoring"),
        new("english", "English", "soft", null),
    ];

    private static readonly Dictionary<string, KnownSkill> ByCompact = BuildIndex();

    private static Dictionary<string, KnownSkill> BuildIndex()
    {
        var index = new Dictionary<string, KnownSkill>(StringComparer.Ordinal);
        foreach (var skill in All)
        {
            foreach (var name in skill.Aliases.Prepend(skill.Name).Prepend(skill.Key))
            {
                var compact = SkillKey.Compact(name);
                if (compact.Length == 0) continue;
                if (index.TryGetValue(compact, out var existing) && existing.Key != skill.Key)
                {
                    throw new InvalidOperationException(
                        $"Skill catalog alias '{name}' is claimed by both '{existing.Key}' and '{skill.Key}'.");
                }
                index[compact] = skill;
            }
        }
        return index;
    }

    /// <summary>
    /// Names that are also everyday words. They count as a mention only in this exact case, so
    /// "React" is the framework and "react quickly" isn't. A null form is never a mention on its
    /// own: "C" and "next" say too little, while "C++" and "Next.js" are their own entries.
    /// </summary>
    private static readonly Dictionary<string, string?> AmbiguousForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = null, ["next"] = null, ["nest"] = null, ["sh"] = null, ["py"] = null, ["es6"] = "ES6",
        ["go"] = "Go", ["swift"] = "Swift", ["rust"] = "Rust", ["ruby"] = "Ruby", ["dart"] = "Dart",
        ["express"] = "Express", ["spring"] = "Spring", ["react"] = "React", ["flask"] = "Flask",
        ["rails"] = "Rails", ["expo"] = "Expo", ["helm"] = "Helm", ["jest"] = "Jest", ["cypress"] = "Cypress",
        ["rabbit"] = "Rabbit", ["oracle"] = "Oracle", ["elastic"] = "Elastic", ["cosmos"] = "Cosmos", ["dynamo"] = "Dynamo",
        ["lambda"] = "Lambda", ["node"] = "Node", ["dapper"] = "Dapper", ["maui"] = "MAUI", ["rest"] = "REST",
        ["ml"] = "ML", ["ts"] = "TS", ["js"] = "JS", ["ef"] = "EF", ["rag"] = "RAG", ["eda"] = "EDA",
    };

    private static readonly Regex Mention = BuildMentionPattern();

    private static Regex BuildMentionPattern()
    {
        var terms = All
            .SelectMany(s => s.Aliases.Prepend(s.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(term => AmbiguousForms.TryGetValue(term, out var form) ? (Term: form, Exact: true) : (Term: term, Exact: false))
            .Where(t => t.Term is not null)
            // Longest first, so "React Native" wins over "React" and "SQL Server" over "SQL".
            .OrderByDescending(t => t.Term!.Length)
            .Select(t =>
            {
                var words = string.Join(@"[\s-]+", t.Term!.Split(' ').Select(Regex.Escape));
                // "Go" alone is the language; "Go to market" isn't.
                if (t.Term == "Go") words += @"(?!\s+to\b)";
                return t.Exact ? $"(?-i:{words})" : words;
            });

        // A name stands alone: "C#/.NET" is two mentions and "Python-based" one; "non-Java" and "Reactive" are none.
        return new Regex(
            @"(?<![\p{L}\p{N}_+#.@-])(?:" + string.Join('|', terms) + @")(?![\p{L}\p{N}_+#]|-(?!based\b)|\.[\p{L}\p{N}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));
    }

    /// <summary>The known skills a piece of text names, in order, each once. Nothing when the text defeats the time limit.</summary>
    public static IReadOnlyList<KnownSkill> MentionsIn(string text)
    {
        var found = new List<KnownSkill>();
        try
        {
            foreach (Match match in Mention.Matches(text))
            {
                if (FindByCompact(SkillKey.Compact(match.Value)) is { } skill && !found.Contains(skill)) found.Add(skill);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
        return found;
    }

    public static KnownSkill? FindByKey(string key) => All.FirstOrDefault(s => s.Key == key);

    /// <summary>The known skill a compacted name or alias stands for, if any.</summary>
    public static KnownSkill? FindByCompact(string compact) => ByCompact.GetValueOrDefault(compact);

    /// <summary>
    /// Autocomplete: an exact name or alias first, then names and aliases that start with the
    /// query, then ones that contain it; shorter names first within each group.
    /// </summary>
    public static IReadOnlyList<KnownSkill> Search(string query, int limit = 8)
    {
        var q = SkillKey.Compact(query);
        if (q.Length == 0) return [];

        return ByCompact
            .Select(entry => (entry.Value, Rank: entry.Key == q ? 0 : entry.Key.StartsWith(q, StringComparison.Ordinal) ? 1 : entry.Key.Contains(q, StringComparison.Ordinal) ? 2 : 3))
            .Where(m => m.Rank < 3)
            .GroupBy(m => m.Value.Key)
            .Select(g => (Skill: g.First().Value, Rank: g.Min(m => m.Rank)))
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.Skill.Name.Length)
            .ThenBy(m => m.Skill.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 25))
            .Select(m => m.Skill)
            .ToList();
    }
}
