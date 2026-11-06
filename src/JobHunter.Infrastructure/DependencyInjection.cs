using JobHunter.Application.Analytics;
using JobHunter.Application.Common;
using JobHunter.Application.Contacts;
using JobHunter.Application.FollowUps;
using JobHunter.Application.Identity;
using JobHunter.Application.Integrations;
using JobHunter.Application.Matching;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Application.Profile;
using JobHunter.Infrastructure.FollowUps;
using JobHunter.Infrastructure.Identity;
using JobHunter.Infrastructure.Integrations;
using JobHunter.Infrastructure.Matching;
using JobHunter.Infrastructure.Opportunities.Imports;
using JobHunter.Infrastructure.Opportunities.JobBoards;
using JobHunter.Infrastructure.Outbox;
using JobHunter.Infrastructure.Persistence;
using JobHunter.Infrastructure.Persistence.Interceptors;
using JobHunter.Infrastructure.Security;
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
        services.AddSingleton<OutboxInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options
                .UseNpgsql(database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>(), sp.GetRequiredService<OutboxInterceptor>());
        });

        AddIdentity(services, configuration);
        services.AddScoped<IProfileDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOpportunitiesDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IContactsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        AddImports(services);
        AddMatching(services);
        AddFollowUps(services);

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

    private static void AddFollowUps(IServiceCollection services)
    {
        services.AddScoped<IFollowUpsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IIntegrationsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddOptions<FollowUpsOptions>().BindConfiguration(FollowUpsOptions.SectionName);

        // Module events: written by the interceptor, delivered by the worker (spec 02 §4).
        services.AddSingleton<OutboxDispatcher>();
        services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName);
        services.AddHostedService<BackgroundWorker>();

        services.AddOptions<SecretsOptions>().BindConfiguration(SecretsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ISecretCipher, AesGcmSecretCipher>();

        // The base URL is the user's, so it goes through the guarded connector like page imports.
        services.AddOptions<OutreachOptions>().BindConfiguration(OutreachOptions.SectionName);
        services.AddHttpClient(OutreachStudioClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<GuardedConnector>()
                .CreateHandler(sp.GetRequiredService<IOptions<OutreachOptions>>().Value.AllowPrivateNetwork));
        services.AddSingleton<IOutreachClient, OutreachStudioClient>();

        services.AddSingleton<AnthropicDraftWriter>();
        services.AddSingleton<IDraftWriter>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.IsConfigured
            ? sp.GetRequiredService<AnthropicDraftWriter>()
            : new UnavailableDraftWriter());
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
