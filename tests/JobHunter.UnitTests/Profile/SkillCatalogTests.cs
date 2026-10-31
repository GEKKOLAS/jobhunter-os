using JobHunter.Domain.Profile;

namespace JobHunter.UnitTests.Profile;

public class SkillCatalogTests
{
    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("c sharp", "csharp")]
    [InlineData("C++", "cplusplus")]
    [InlineData("ASP.NET Core", "aspnetcore")]
    [InlineData("  Node.js ", "nodejs")]
    [InlineData("!!!", "")]
    public void Compact_keeps_letters_and_digits_and_spells_out_sharp_and_plus(string name, string compact)
    {
        Assert.Equal(compact, SkillKey.Compact(name));
    }

    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("csharp", "csharp")]
    [InlineData(".NET Core", "dotnet")]
    [InlineData(".NET", "dotnet")]
    [InlineData("Postgres", "postgresql")]
    [InlineData("k8s", "kubernetes")]
    [InlineData("ReactJS", "react")]
    [InlineData("EF Core", "efcore")]
    [InlineData("Golang", "go")]
    public void Known_names_and_aliases_fold_into_the_catalog_key(string name, string key)
    {
        Assert.Equal(key, SkillKey.For(name));
    }

    [Fact]
    public void Unknown_skills_keep_their_compact_form()
    {
        Assert.Equal("quantumbasketweaving", SkillKey.For("Quantum Basket-Weaving"));
    }

    [Fact]
    public void Every_alias_belongs_to_one_skill_and_every_entry_is_well_formed()
    {
        // Building the index throws on a shared alias; touching the catalog forces it.
        Assert.NotEmpty(SkillCatalog.Search("a"));

        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Select(s => s.Key).Distinct().Count());
        foreach (var skill in SkillCatalog.All)
        {
            Assert.Contains(skill.Category, ProfileVocabulary.SkillCategories);
            Assert.Equal(skill.Key, SkillKey.For(skill.Name));
            Assert.Matches("^[a-z0-9]+$", skill.Key);
        }
    }

    [Fact]
    public void Distinct_tools_are_not_merged_by_an_alias()
    {
        Assert.NotEqual(SkillKey.For("NUnit"), SkillKey.For("xUnit"));
        Assert.NotEqual(SkillKey.For("GitHub"), SkillKey.For("Git"));
        Assert.NotEqual(SkillKey.For("Azure Functions"), SkillKey.For("AWS Lambda"));
    }

    [Fact]
    public void Search_ranks_exact_then_prefix_then_contains()
    {
        var results = SkillCatalog.Search("react").Select(s => s.Key).ToList();

        Assert.Equal("react", results[0]);
        Assert.Contains("reactnative", results);
        Assert.True(results.IndexOf("react") < results.IndexOf("reactnative"));
    }

    [Fact]
    public void Search_finds_skills_by_alias_and_ignores_empty_queries()
    {
        Assert.Equal("kubernetes", SkillCatalog.Search("k8s")[0].Key);
        Assert.Empty(SkillCatalog.Search("  "));
        Assert.Equal(3, SkillCatalog.Search("s", limit: 3).Count);
    }
}
