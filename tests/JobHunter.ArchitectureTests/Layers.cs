using System.Reflection;
using NetArchTest.Rules;

namespace JobHunter.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(JobHunter.Domain.Common.DomainException).Assembly;
    public static readonly Assembly Application = typeof(JobHunter.Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(JobHunter.Infrastructure.DependencyInjection).Assembly;

    public static string Offenders(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
