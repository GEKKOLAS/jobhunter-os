using FluentValidation;
using JobHunter.Application.Identity;
using JobHunter.Application.Profile;
using JobHunter.Application.Profile.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Identity
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<PasswordResetHandler>();
        services.AddScoped<SessionsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<AuditLogsHandler>();

        // Profile
        services.AddSingleton<ISkillCatalog, SkillCatalogService>();
        services.AddScoped<ProfileHandler>();
        services.AddScoped<SkillsHandler>();
        services.AddScoped<EvidenceHandler>();

        return services;
    }
}
