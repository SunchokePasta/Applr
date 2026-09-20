using Applr.Services.DTOs;

namespace Applr.Services.Interfaces;

public interface IGmailService
{
    Task<List<GmailMessageSummaryDto>> GetLatestMessagesAsync(
        int count = 10,
        CancellationToken cancellationToken = default);
}
