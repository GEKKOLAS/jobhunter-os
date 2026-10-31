using JobHunter.Application.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JobHunter.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL 16 container; migrations run through
/// the startup gate. One container serves the whole <see cref="ApiCollection"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Testcontainers stays on 3.x like Mail Template Hub (spec 04, testing strategy).
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("jobhunter_test")
        .WithUsername("jh")
        .WithPassword("jh_test_password")
        .Build();

    public RecordingResetNotifier ResetLinks { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting, not in-memory configuration: Program reads these while the host is built.
        builder.UseSetting("Database:ConnectionString", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Auth:SessionCookieName", TestSession.SessionCookie);
        builder.UseSetting("Auth:SecureCookies", "false");
        builder.UseSetting("Auth:WebBaseUrl", "http://web.test");
        // Every test client shares one IP; tests that exercise the limit lower it themselves.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");

        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPasswordResetNotifier>(ResetLinks)));
    }

    public TestSession NewSession() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));

    /// <summary>A brand-new user, signed in.</summary>
    public async Task<TestSession> SignedInAsync(string displayName = "Ada Lovelace")
    {
        var session = NewSession();
        var response = await session.PostAsync("/api/v1/auth/register", new
        {
            email = $"user-{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName,
        });
        response.EnsureSuccessStatusCode();
        return session;
    }

    async Task IAsyncLifetime.InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
