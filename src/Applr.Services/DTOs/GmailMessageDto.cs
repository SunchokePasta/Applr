namespace Applr.Services.DTOs;

/// <summary>
/// A message as Gmail returns it with format=metadata: headers and snippet,
/// no body. Only the fields the app reads are declared.
/// </summary>
public sealed class GmailMessageDto
{
    public string Id { get; set; } = string.Empty;

    public string ThreadId { get; set; } = string.Empty;

    public string Snippet { get; set; } = string.Empty;

    /// <summary>Milliseconds since the Unix epoch, as a string -- Gmail sends it quoted.</summary>
    public string InternalDate { get; set; } = string.Empty;

    public List<string> LabelIds { get; set; } = [];

    public GmailPayloadDto? Payload { get; set; }
}
