namespace JobHunter.Application.Contacts.Contracts;

/// <summary>What another module may know about one of the signed-in user's contacts.</summary>
public sealed record ContactFacts(Guid Id, string Name, string? Role, string? Email, bool Archived);

public interface IContactFacts
{
    Task<ContactFacts?> FindAsync(Guid id, CancellationToken ct);
}

/// <summary>Adds to a contact's conversation log on the user's behalf, for what other modules do (an email sent).</summary>
public interface IInteractionLog
{
    /// <summary>
    /// Logs an outbound email. Doesn't save: the request's one unit of work saves it with the
    /// caller's changes, so a sent draft and its log entry commit together.
    /// </summary>
    Task LogSentEmailAsync(Guid contactId, Guid? opportunityId, string summary, string? outreachSendId, DateTimeOffset at, CancellationToken ct);
}
