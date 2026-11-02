using System.Threading.RateLimiting;
using JobHunter.Api;
using JobHunter.Api.Auth;
using JobHunter.Api.Logging;
using JobHunter.Api.Middleware;
using JobHunter.Application;
using JobHunter.Application.Common;
using JobHunter.Infrastructure;
using JobHunter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.With<SecretScrubbingEnricher>());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// --- Identity: cookie sessions + CSRF (spec 02 §5) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());
builder.Services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<HttpCurrentUser>());
builder.Services.AddSingleton<AuthCookies>();

builder.Services
    .AddAuthentication(SessionAuthentication.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthentication.Scheme, null);

// Secure by default: an endpoint is anonymous only when it says [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder(SessionAuthentication.Scheme).RequireAuthenticatedUser().Build());

// --- Rate limits per client IP; resolved per request so configuration overrides apply ---
builder.Services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static RateLimitPartition<string> Fixed(
        HttpContext context, Func<RateLimitingOptions, RateLimitingOptions.PolicyOptions> select)
    {
        var policy = select(context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromMinutes(policy.WindowMinutes),
                QueueLimit = 0,
            });
    }

    options.AddPolicy("auth", context => Fixed(context, o => o.Auth));
    options.AddPolicy("import", context => Fixed(context, o => o.Import));
});

var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("JobHunter.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

// Outermost, so the log shows the status the client really got, not an exception passing through.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
// Bodiless errors (unknown routes, 401/403 challenges, 405s, 429s) still answer with ProblemDetails.
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapControllers();

// Development and test startup gate; production applies migrations in the deploy pipeline.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (database.ApplyMigrationsOnStartup)
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}

app.Run();

/// <summary>Entry point, public for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
