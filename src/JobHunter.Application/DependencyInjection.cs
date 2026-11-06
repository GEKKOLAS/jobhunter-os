using FluentValidation;
using JobHunter.Application.Analytics;
using JobHunter.Application.Common;
using JobHunter.Application.Contacts;
using JobHunter.Application.Contacts.Contracts;
using JobHunter.Application.FollowUps;
using JobHunter.Application.Identity;
using JobHunter.Application.Identity.Contracts;
using JobHunter.Application.Integrations;
using JobHunter.Application.Integrations.Contracts;
using JobHunter.Application.Matching;
using JobHunter.Application.Matching.Contracts;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Application.Profile;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Contacts.Events;
using JobHunter.Domain.Opportunities.Events;
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
        services.AddScoped<IUserFacts, UserFactsService>();
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
        services.AddScoped<ContactContractsService>();
        services.AddScoped<IContactFacts>(sp => sp.GetRequiredService<ContactContractsService>());
        services.AddScoped<IInteractionLog>(sp => sp.GetRequiredService<ContactContractsService>());

        // FollowUps. Event handlers run from the outbox, with the user taken from each event.
        services.AddScoped<RemindersHandler>();
        services.AddScoped<NotificationsHandler>();
        services.AddScoped<DraftsHandler>();
        services.AddScoped<DueRemindersPass>();
        services.AddScoped<IEventHandler<OpportunityStageChanged>, ReminderRules>();
        services.AddScoped<IEventHandler<InteractionLogged>, ReminderRules>();
        services.AddScoped<IEventHandler<InteractionLogged>, ResponseRecorder>();

        // Integrations
        services.AddScoped<OutreachHandler>();
        services.AddScoped<IOutreachSender, OutreachSender>();
        services.AddScoped<AiCapabilitiesHandler>();

        // Matching
        services.AddScoped<RequirementsHandler>();
        services.AddScoped<FitHandler>();
        services.AddScoped<IFitScores>(sp => sp.GetRequiredService<FitHandler>());
        services.AddSingleton<RuleBasedRequirementExtractor>();

        // Analytics
        services.AddScoped<AnalyticsHandler>();
        services.AddScoped<DashboardHandler>();

        return services;
    }
}
