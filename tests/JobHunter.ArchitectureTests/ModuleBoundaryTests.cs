using System.Reflection;
using NetArchTest.Rules;

namespace JobHunter.ArchitectureTests;

/// <summary>
/// The module boundary rule of docs/spec/02-architecture.md §3: a module uses another one
/// only through its <c>Contracts</c> and domain <c>Events</c>. Checked in Domain and
/// Application; Infrastructure is where every module's adapters meet.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] Modules =
        ["Identity", "Profile", "Opportunities", "Matching", "Contacts", "FollowUps", "Analytics", "Integrations"];

    /// <summary>Namespaces under a layer root that belong to no module.</summary>
    private const string SharedKernel = "Common";

    /// <summary>What a module exposes to the others.</summary>
    private static readonly string[] PublicSurfaces = ["Contracts", "Events"];

    /// <summary>Analytics only reads, so it may query other modules' entities (but not call their use cases).</summary>
    private const string ReadOnlyModule = "Analytics";

    private static readonly (Assembly Assembly, string Root)[] CheckedLayers =
    [
        (Layers.Domain, "JobHunter.Domain"),
        (Layers.Application, "JobHunter.Application"),
    ];

    public static TheoryData<string> ModuleNames => new(Modules);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Modules_use_each_other_only_through_contracts_and_events(string module)
    {
        var forbidden = CheckedLayers
            .Where(layer => !(module == ReadOnlyModule && layer.Assembly == Layers.Domain))
            .SelectMany(layer => layer.Assembly.GetTypes().Select(type => (Type: type, layer.Root)))
            .Where(t => ModuleOf(t.Type, t.Root) is { } owner && owner != module && !IsPublicSurface(t.Type, t.Root, owner))
            .Select(t => t.Type.FullName!)
            .ToArray();

        foreach (var (assembly, root) in CheckedLayers)
        {
            if (forbidden.Length == 0) continue;

            var result = Types.InAssembly(assembly)
                .That().ResideInNamespace($"{root}.{module}")
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful, Layers.Offenders(result));
        }
    }

    [Fact]
    public void Every_namespace_is_a_declared_module_or_the_shared_kernel()
    {
        var unknown = CheckedLayers
            .SelectMany(layer => layer.Assembly.GetTypes().Select(type => (Type: type, layer.Root)))
            .Select(t => FirstSegment(t.Type, t.Root))
            .Where(segment => segment is not null && segment != SharedKernel && !Modules.Contains(segment))
            .Distinct()
            .ToList();

        Assert.True(unknown.Count == 0,
            $"Undeclared module namespaces: {string.Join(", ", unknown)}. Add them to {nameof(Modules)} and to spec 02 §3.");
    }

    private static string? FirstSegment(Type type, string root)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(root + ".", StringComparison.Ordinal)) return null;
        // Compiler-generated helpers (e.g. <PrivateImplementationDetails>) have no namespace and are skipped above.
        return ns[(root.Length + 1)..].Split('.')[0];
    }

    private static string? ModuleOf(Type type, string root) =>
        FirstSegment(type, root) is { } segment && Modules.Contains(segment) ? segment : null;

    private static bool IsPublicSurface(Type type, string root, string module) =>
        PublicSurfaces.Any(surface =>
            type.Namespace == $"{root}.{module}.{surface}" ||
            type.Namespace!.StartsWith($"{root}.{module}.{surface}.", StringComparison.Ordinal));
}
