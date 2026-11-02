using JobHunter.Application.Common;
using JobHunter.Application.Identity;
using JobHunter.Application.Matching;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Application.Profile;
using JobHunter.Infrastructure.Identity;
using JobHunter.Infrastructure.Matching;
using JobHunter.Infrastructure.Opportunities.Imports;
using JobHunter.Infrastructure.Opportunities.JobBoards;
using JobHunter.Infrastructure.Persistence;
using JobHunter.Infrastructure.Persistence.Interceptors;
using JobHunter.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The HTTP client name for page imports; tests give it a stub handler.</summary>
    public const string PostingFetcherClient = "posting-fetcher";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TimestampInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options
                .UseNpgsql(database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());
        });

        AddIdentity(services, configuration);
        services.AddScoped<IProfileDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOpportunitiesDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        AddImports(services);
        AddMatching(services);

        return services;
    }

    private static void AddMatching(IServiceCollection services)
    {
        services.AddScoped<IMatchingDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddOptions<AiOptions>().BindConfiguration(AiOptions.SectionName);

        // With a key, Claude reads postings; without one, the rules do. Either way the user confirms.
        services.AddHttpClient(AnthropicRequirementExtractor.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddSingleton<AnthropicRequirementExtractor>();
        services.AddSingleton<IRequirementExtractor>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.IsConfigured
            ? sp.GetRequiredService<AnthropicRequirementExtractor>()
            : sp.GetRequiredService<RuleBasedRequirementExtractor>());
    }

    private static void AddImports(IServiceCollection services)
    {
        services.AddOptions<ImportOptions>().BindConfiguration(ImportOptions.SectionName);
        services.AddSingleton<IPostingParser, PostingParser>();

        // User-supplied URLs only ever go through the guarded connector (spec 02 §5).
        services.AddSingleton<IHostResolver, DnsHostResolver>();
        services.AddSingleton<GuardedConnector>();
        services.AddHttpClient<IPostingFetcher, SafePostingFetcher>(PostingFetcherClient, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<GuardedConnector>().CreateHandler());

        // Fixed base URLs, so no guard; a large board with full text is several MB.
        services.AddHttpClient(JobBoardClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 32 * 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JobHunterOS/0.1 (job search tool)");
        });
        services.AddSingleton<IJobBoardClient, GreenhouseClient>();
        services.AddSingleton<IJobBoardClient, LeverClient>();
        services.AddSingleton<IJobBoardClient, AshbyClient>();
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        if (configuration.GetValue<bool>($"{AuthOptions.SectionName}:{nameof(AuthOptions.LogPasswordResetLinks)}"))
        {
            services.AddSingleton<IPasswordResetNotifier, LoggingPasswordResetNotifier>();
        }
        else
        {
            services.AddSingleton<IPasswordResetNotifier, UnconfiguredPasswordResetNotifier>();
        }
    }
}
