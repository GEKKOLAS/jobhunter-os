using NetArchTest.Rules;

namespace JobHunter.ArchitectureTests;

/// <summary>The dependency rules of docs/spec/02-architecture.md §2.</summary>
public class LayerDependencyTests
{
    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot().HaveDependencyOnAny("JobHunter.Application", "JobHunter.Infrastructure", "JobHunter.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Layers.Offenders(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot().HaveDependencyOnAny("JobHunter.Infrastructure", "JobHunter.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Layers.Offenders(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Layers.Infrastructure)
            .ShouldNot().HaveDependencyOn("JobHunter.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Layers.Offenders(result));
    }
}
