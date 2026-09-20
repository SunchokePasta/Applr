namespace Applr.Services.DTOs;

/// <summary>What the API returns for one email: the parts worth listing.</summary>
public sealed class GmailMessageSummaryDto
{
    public string Id { get; set; } = string.Empty;

    public string ThreadId { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Snippet { get; set; } = string.Empty;

    public DateTimeOffset? ReceivedAt { get; set; }
}
