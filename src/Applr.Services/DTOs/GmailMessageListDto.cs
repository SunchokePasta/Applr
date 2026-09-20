namespace Applr.Services.DTOs;

public sealed class GmailMessageListDto
{
    /// <summary>Absent from Gmail's response, not empty, when the mailbox has no messages.</summary>
    public List<GmailMessageRefDto> Messages { get; set; } = [];

    public string? NextPageToken { get; set; }

    public int ResultSizeEstimate { get; set; }
}
