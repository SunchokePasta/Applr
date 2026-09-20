using Applr.Services.DTOs;
using Refit;

namespace Applr.Services.APIs;

/// <summary>
/// The slice of the Gmail REST API the app uses. Base address comes from
/// "GmailApiBaseUrl" in appsettings and has no trailing slash, because Refit
/// prepends the base path to each route below.
///
/// No authorisation is wired yet: Gmail answers 401 until an OAuth access
/// token is attached, which will be done with a message handler on this
/// client in Program.cs rather than a parameter on every method.
/// </summary>
public interface IGmailApi
{
    /// <summary>
    /// Message ids only, newest first. Gmail returns no bodies or headers
    /// from the list call, so each id is then fetched with GetMessageAsync.
    /// </summary>
    [Get("/users/me/messages")]
    Task<GmailMessageListDto> ListMessagesAsync(
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One message. format=metadata returns headers and the snippet without
    /// the body; metadataHeaders limits which headers, and is repeated once
    /// per value on the query string, which is the form Gmail expects.
    /// </summary>
    [Get("/users/me/messages/{id}")]
    Task<GmailMessageDto> GetMessageAsync(
        string id,
        string format,
        [Query(CollectionFormat.Multi)] IEnumerable<string> metadataHeaders,
        CancellationToken cancellationToken = default);
}
