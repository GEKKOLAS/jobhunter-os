using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Integrations.Contracts;
using JobHunter.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Application.Integrations;

/// <summary>The Integrations module's tables (spec 02 §3, one database port per module).</summary>
public interface IIntegrationsDbContext
{
    DbSet<OutreachConnection> OutreachConnections { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class OutreachOptions
{
    public const string SectionName = "Integrations:Outreach";

    /// <summary>
    /// Lets the base URL be http and point at loopback or private addresses. Development only,
    /// where Outreach Studio runs on localhost; otherwise the SSRF guard applies (spec 02 §7).
    /// </summary>
    public bool AllowPrivateNetwork { get; init; }
}

public enum OutreachFailure
{
    Unauthorized,
    Rejected,
    Unavailable,
}

/// <summary>An Outreach Studio call that didn't succeed, with words fit to show the user.</summary>
public sealed class OutreachClientException(OutreachFailure failure, string message) : Exception(message)
{
    public OutreachFailure Failure { get; } = failure;
}

public sealed record OutreachAccount(string DisplayName, Guid? DefaultAccountId);

public sealed record OutreachSendRequest(Guid AccountId, Guid TemplateVersionId, string ToEmail, string? ToName, string Subject, string Body);

/// <summary>The HTTP side of Outreach Studio's public API, implemented in Infrastructure.</summary>
public interface IOutreachClient
{
    Task<OutreachAccount> GetMeAsync(string baseUrl, string apiKey, CancellationToken ct);

    /// <returns>The send's id.</returns>
    Task<string> SendAsync(string baseUrl, string apiKey, OutreachSendRequest request, string idempotencyKey, CancellationToken ct);
}

/// <param name="KeyHint">The saved key's last four characters. The key itself never leaves the server.</param>
public sealed record OutreachSettingsDto(
    bool Configured, string? BaseUrl, Guid? TemplateVersionId, string? KeyHint, DateTimeOffset? LastTestedAt, bool? LastTestOk);

public sealed record OutreachTestDto(bool Ok, string? DisplayName, bool HasDefaultMailbox, string? Message);

public sealed record OutreachInput(string BaseUrl, string? ApiKey, Guid TemplateVersionId);

public sealed class OutreachInputValidator : AbstractValidator<OutreachInput>
{
    public OutreachInputValidator(IOptions<OutreachOptions> options)
    {
        var allowHttp = options.Value.AllowPrivateNetwork;
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(300)
            .Must(u => Uri.TryCreate(u?.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || (allowHttp && uri.Scheme == Uri.UriSchemeHttp))
                && uri.UserInfo.Length == 0 && uri.Query.Length == 0)
            .WithMessage(allowHttp ? "Use Outreach Studio's address, like http://localhost:5001." : "Use Outreach Studio's https address.");
        RuleFor(x => x.ApiKey).MaximumLength(200)
            .Must(k => k!.Trim().StartsWith("mth_", StringComparison.Ordinal)).When(x => !string.IsNullOrWhiteSpace(x.ApiKey))
            .WithMessage("Use an Outreach Studio API key: it starts with mth_.");
        RuleFor(x => x.TemplateVersionId).NotEmpty().WithMessage("Paste the id of the template version to send with.");
    }
}

public static class IntegrationsAudit
{
    public const string Configured = "outreach.configured";
    public const string Tested = "outreach.tested";
    public const string Removed = "outreach.removed";
}

public sealed class OutreachHandler(
    IIntegrationsDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    ISecretCipher cipher,
    IOutreachClient client,
    IValidator<OutreachInput> validator)
{
    public async Task<OutreachSettingsDto> GetAsync(CancellationToken ct) => ToDto(await FindAsync(ct));

    public async Task<OutreachSettingsDto> SaveAsync(OutreachInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var connection = await FindAsync(ct);
        var apiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? null : input.ApiKey.Trim();
        var baseUrl = input.BaseUrl.Trim().TrimEnd('/');

        if (connection is null)
        {
            if (apiKey is null) throw new InvalidInputException("outreach.key_required", "Paste an API key from Outreach Studio.");

            var id = Guid.CreateVersion7();
            var (wrapped, version) = cipher.CreateDataKey();
            var secret = cipher.Encrypt(wrapped, version, apiKey, id);
            connection = new OutreachConnection
            {
                Id = id,
                UserId = currentUser.UserId,
                BaseUrl = baseUrl,
                TemplateVersionId = input.TemplateVersionId,
                WrappedDataKey = wrapped,
                KeyVersion = version,
                ApiKeyCiphertext = secret.Ciphertext,
                ApiKeyNonce = secret.Nonce,
                KeyHint = Hint(apiKey),
            };
            db.OutreachConnections.Add(connection);
        }
        else
        {
            // A new address or template hasn't been tried yet.
            if (connection.BaseUrl != baseUrl || connection.TemplateVersionId != input.TemplateVersionId || apiKey is not null)
            {
                connection.LastTestedAt = null;
                connection.LastTestOk = null;
            }
            connection.BaseUrl = baseUrl;
            connection.TemplateVersionId = input.TemplateVersionId;
            if (apiKey is not null)
            {
                // A fresh data key with every new secret: nothing old can decrypt it.
                var (wrapped, version) = cipher.CreateDataKey();
                var secret = cipher.Encrypt(wrapped, version, apiKey, connection.Id);
                connection.WrappedDataKey = wrapped;
                connection.KeyVersion = version;
                connection.ApiKeyCiphertext = secret.Ciphertext;
                connection.ApiKeyNonce = secret.Nonce;
                connection.KeyHint = Hint(apiKey);
            }
        }

        audit.Add(IntegrationsAudit.Configured, currentUser.UserId, "outreach_connection", connection.Id, new { keyChanged = apiKey is not null });
        await db.SaveChangesAsync(ct);
        return ToDto(connection);
    }

    public async Task<OutreachTestDto> TestAsync(CancellationToken ct)
    {
        var connection = await FindAsync(ct) ?? throw new ConflictException("outreach.not_configured", "Connect Outreach Studio first.");

        OutreachTestDto result;
        try
        {
            var me = await client.GetMeAsync(connection.BaseUrl, DecryptKey(cipher, connection), ct);
            result = new OutreachTestDto(true, me.DisplayName, me.DefaultAccountId is not null,
                me.DefaultAccountId is null ? "Connected, but no mailbox is set as default there yet. Sending needs one." : null);
        }
        catch (OutreachClientException ex)
        {
            result = new OutreachTestDto(false, null, false, ex.Message);
        }

        connection.LastTestedAt = clock.UtcNow;
        connection.LastTestOk = result.Ok;
        audit.Add(IntegrationsAudit.Tested, currentUser.UserId, "outreach_connection", connection.Id, new { result.Ok });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task RemoveAsync(CancellationToken ct)
    {
        var connection = await FindAsync(ct);
        if (connection is null) return;
        db.OutreachConnections.Remove(connection);
        audit.Add(IntegrationsAudit.Removed, currentUser.UserId, "outreach_connection", connection.Id);
        await db.SaveChangesAsync(ct);
    }

    internal static string DecryptKey(ISecretCipher cipher, OutreachConnection c) =>
        cipher.Decrypt(c.WrappedDataKey, c.KeyVersion, new EncryptedSecret(c.ApiKeyCiphertext, c.ApiKeyNonce), c.Id);

    private Task<OutreachConnection?> FindAsync(CancellationToken ct) =>
        db.OutreachConnections.FirstOrDefaultAsync(c => c.UserId == currentUser.UserId, ct);

    private static string Hint(string apiKey) => apiKey.Length <= 4 ? "" : apiKey[^4..];

    private static OutreachSettingsDto ToDto(OutreachConnection? c) => c is null
        ? new OutreachSettingsDto(false, null, null, null, null, null)
        : new OutreachSettingsDto(true, c.BaseUrl, c.TemplateVersionId, c.KeyHint, c.LastTestedAt, c.LastTestOk);
}

internal sealed class OutreachSender(IIntegrationsDbContext db, ICurrentUser currentUser, ISecretCipher cipher, IOutreachClient client) : IOutreachSender
{
    public Task<bool> IsConfiguredAsync(CancellationToken ct) =>
        db.OutreachConnections.AnyAsync(c => c.UserId == currentUser.UserId, ct);

    public async Task<OutreachSendResult> SendAsync(OutreachEmail email, CancellationToken ct)
    {
        var connection = await db.OutreachConnections.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == currentUser.UserId, ct)
            ?? throw new ConflictException("outreach.not_configured", "Connect Outreach Studio in Settings, or copy it and mark it sent.");
        var apiKey = OutreachHandler.DecryptKey(cipher, connection);

        try
        {
            // Read fresh each time: the default mailbox is the user's choice in Outreach Studio.
            var me = await client.GetMeAsync(connection.BaseUrl, apiKey, ct);
            if (me.DefaultAccountId is not { } accountId)
            {
                throw new ConflictException("outreach.no_mailbox", "Set a default mailbox in Outreach Studio first.");
            }

            var sendId = await client.SendAsync(connection.BaseUrl, apiKey,
                new OutreachSendRequest(accountId, connection.TemplateVersionId, email.ToEmail, email.ToName, email.Subject, email.Body),
                $"jobhunter-draft-{email.DraftId}", ct);
            return new OutreachSendResult(sendId);
        }
        catch (OutreachClientException ex) when (ex.Failure == OutreachFailure.Unavailable)
        {
            throw new UpstreamUnavailableException("outreach.unavailable", ex.Message);
        }
        catch (OutreachClientException ex)
        {
            throw new InvalidInputException("outreach.rejected", ex.Message);
        }
    }
}

public sealed record AiCapabilitiesDto(string Extraction, bool Drafts);

public sealed class AiCapabilitiesHandler(IOptions<AiOptions> options)
{
    public AiCapabilitiesDto Get() => new(options.Value.IsConfigured ? "ai" : "rules", options.Value.IsConfigured);
}
