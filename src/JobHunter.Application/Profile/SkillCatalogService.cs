using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Profile;

namespace JobHunter.Application.Profile;

internal sealed class SkillCatalogService : ISkillCatalog
{
    public string KeyFor(string name) => SkillKey.For(name);

    public CatalogSkill? Find(string key) => SkillCatalog.FindByKey(key) is { } skill ? ToContract(skill) : null;

    public IReadOnlyList<CatalogSkill> Search(string query, int limit) =>
        SkillCatalog.Search(query, limit).Select(ToContract).ToList();

    public IReadOnlyList<CatalogSkill> MentionsIn(string text) =>
        SkillCatalog.MentionsIn(text).Select(ToContract).ToList();

    private static CatalogSkill ToContract(KnownSkill skill) => new(skill.Key, skill.Name, skill.Category, skill.Family);
}
