namespace JobHunter.Domain.Contacts;

/// <summary>The closed value sets and limits of the Contacts module (spec 03, Contacts). Stored as text.</summary>
public static class ContactVocabulary
{
    public static readonly IReadOnlyList<string> Relations = ["recruiter", "hiring_manager", "referral", "interviewer", "other"];
    public static readonly IReadOnlyList<string> Channels = ["email", "linkedin", "phone", "video", "in_person", "other"];
    public static readonly IReadOnlyList<string> Directions = ["outbound", "inbound"];

    public const int MaxName = 120;
    public const int MaxRole = 120;
    public const int MaxEmail = 254;
    public const int MaxLinkedInUrl = 300;
    public const int MaxPhone = 40;
    public const int MaxNotes = 2000;
    public const int MaxSummary = 2000;
}

public static class ContactRules
{
    /// <summary>What a user pastes, ready to check: trimmed, and with https added when it has no scheme at all.</summary>
    public static string? NormalizeLinkedInUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        return url.Contains("://", StringComparison.Ordinal) ? url : $"https://{url}";
    }

    /// <summary>https on linkedin.com or a subdomain of it, so "linkedin.com.evil.example" and "evil-linkedin.com" don't pass.</summary>
    public static bool IsLinkedInUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && (uri.Host == "linkedin.com" || uri.Host.EndsWith(".linkedin.com", StringComparison.Ordinal));
}
