using JobHunter.Application.Common;
using JobHunter.Application.Identity;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Profile;
using JobHunter.Infrastructure.Identity;
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

        return services;
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
