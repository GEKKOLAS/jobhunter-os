namespace JobHunter.Application.Integrations.Contracts;

/// <param name="DraftId">Makes the send idempotent: the same draft can't go out twice.</param>
public sealed record OutreachEmail(Guid DraftId, string ToEmail, string? ToName, string Subject, string Body);

public sealed record OutreachSendResult(string SendId);

/// <summary>Sends an approved email through the user's Outreach Studio (spec 02 §7).</summary>
public interface IOutreachSender
{
    Task<bool> IsConfiguredAsync(CancellationToken ct);

    /// <summary>
    /// Throws <c>ConflictException</c> (<c>outreach.not_configured</c>, <c>outreach.no_mailbox</c>),
    /// <c>InvalidInputException</c> (<c>outreach.rejected</c>) or
    /// <c>UpstreamUnavailableException</c> (<c>outreach.unavailable</c>).
    /// </summary>
    Task<OutreachSendResult> SendAsync(OutreachEmail email, CancellationToken ct);
}
