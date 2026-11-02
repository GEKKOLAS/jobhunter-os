using FluentValidation;
using JobHunter.Application.Contacts;
using JobHunter.Application.Identity;
using JobHunter.Application.Matching;
using JobHunter.Application.Matching.Contracts;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Application.Opportunities.Imports;
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
        services.AddScoped<ICandidateFacts, CandidateFactsService>();

        // Opportunities
        services.AddScoped<CompanyResolver>();
        services.AddScoped<OpportunitiesHandler>();
        services.AddScoped<CompaniesHandler>();
        services.AddScoped<BoardHandler>();
        services.AddScoped<ImportsHandler>();
        services.AddScoped<SourcesHandler>();
        services.AddScoped<IOpportunityFacts, OpportunityFactsService>();
        services.AddScoped<ICompanyDirectory, CompanyDirectoryService>();

        // Contacts
        services.AddScoped<ContactsHandler>();
        services.AddScoped<InteractionsHandler>();

        // Matching
        services.AddScoped<RequirementsHandler>();
        services.AddScoped<FitHandler>();
        services.AddScoped<IFitScores>(sp => sp.GetRequiredService<FitHandler>());
        services.AddSingleton<RuleBasedRequirementExtractor>();

        return services;
    }
}
