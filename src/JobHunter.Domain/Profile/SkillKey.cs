using System.Text;

namespace JobHunter.Domain.Profile;

/// <summary>
/// The identity of a skill name (spec 03, Profile): compact it, then fold catalog aliases,
/// so "C#", "csharp" and "C Sharp" are one skill and ".NET Core" is <c>dotnet</c>.
/// </summary>
public static class SkillKey
{
    /// <summary>
    /// Lowercase, <c>#</c> → <c>sharp</c>, <c>+</c> → <c>plus</c>, every other character that
    /// isn't a letter or digit dropped. Empty when nothing is left.
    /// </summary>
    public static string Compact(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) builder.Append(c);
            else if (c == '#') builder.Append("sharp");
            else if (c == '+') builder.Append("plus");
        }
        return builder.ToString();
    }

    /// <summary>The catalog key when the name is a known skill or alias; otherwise its compact form.</summary>
    public static string For(string name)
    {
        var compact = Compact(name);
        return SkillCatalog.FindByCompact(compact)?.Key ?? compact;
    }
}
