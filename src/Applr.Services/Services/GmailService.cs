using Applr.Services.APIs;
using Applr.Services.DTOs;
using Applr.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Applr.Services.Services;

public sealed class GmailService : IGmailService
{
    private static readonly string[] SummaryHeaders = ["From", "Subject"];

    private readonly IGmailApi _gmailApi;
    private readonly ILogger<GmailService> _logger;

    public GmailService(
        IGmailApi gmailApi,
        ILogger<GmailService> logger)
    {
        _gmailApi = gmailApi;
        _logger = logger;
    }

    public async Task<List<GmailMessageSummaryDto>> GetLatestMessagesAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting the latest {Count} messages from the Gmail API.",
            count);

        var list = await _gmailApi.ListMessagesAsync(
            count,
            cancellationToken);

        // The list call returns ids only, so each message is fetched for its
        // headers. Ten calls in parallel, well inside Gmail's per-second quota.
        var messages = await Task.WhenAll(list.Messages.Select(message =>
            _gmailApi.GetMessageAsync(
                message.Id,
                "metadata",
                SummaryHeaders,
                cancellationToken)));

        _logger.LogInformation(
            "Received {MessageCount} messages from the Gmail API.",
            messages.Length);

        return messages.Select(ToSummary).ToList();
    }

    private static GmailMessageSummaryDto ToSummary(GmailMessageDto message) => new()
    {
        Id = message.Id,
        ThreadId = message.ThreadId,
        From = Header(message, "From"),
        Subject = Header(message, "Subject"),
        Snippet = message.Snippet,
        ReceivedAt = long.TryParse(message.InternalDate, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null,
    };

    private static string Header(GmailMessageDto message, string name) =>
        message.Payload?.Headers
            .FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? string.Empty;
}
