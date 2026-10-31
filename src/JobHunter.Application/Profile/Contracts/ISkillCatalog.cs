namespace JobHunter.Application.Profile.Contracts;

/// <summary>A catalog entry as other modules see it.</summary>
/// <param name="Family">Skills that partly transfer to each other, or null.</param>
public sealed record CatalogSkill(string Key, string Name, string Category, string? Family);

/// <summary>
/// The skill catalog, exposed by Profile to the other modules (Matching uses it to key
/// requirements and to find related skills).
/// </summary>
public interface ISkillCatalog
{
    /// <summary>The key a skill name gets: the catalog key for known names and aliases, else its compact form.</summary>
    string KeyFor(string name);

    CatalogSkill? Find(string key);

    IReadOnlyList<CatalogSkill> Search(string query, int limit);
}
