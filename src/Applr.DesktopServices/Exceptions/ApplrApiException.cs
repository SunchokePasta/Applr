namespace Applr.DesktopServices.Exceptions;

/// <summary>
/// The one exception type ApplrClient throws. It carries a message that
/// is already safe and sensible to put in front of a user, plus the
/// reference id that ties it to a log line.
///
/// Why a dedicated type rather than letting HttpRequestException travel:
/// by the time an HttpRequestException reaches the UI layer, the useful
/// part (Applr.API's own explanation of what broke, sitting in the
/// response body) has already been thrown away, and what's left --
/// "Response status code does not indicate success: 503" -- is not
/// something to show a person. ApplrClient reads the body first and
/// repackages it as one of these, so MainWindow can post
/// <see cref="UserMessage"/> straight to the WebView without deciding
/// what is and isn't safe to display.
///
/// <see cref="Reference"/> is Applr.API's reference id when the API
/// answered, and a locally minted one when it didn't answer at all --
/// either way it appears in a log file somewhere.
/// </summary>
public sealed class ApplrApiException : Exception
{
    public ApplrApiException(
        string userMessage,
        string reference,
        Exception? innerException = null)
        : base($"{userMessage} (reference {reference})", innerException)
    {
        UserMessage = userMessage;
        Reference = reference;
    }

    /// <summary>Safe to show a user, as-is.</summary>
    public string UserMessage { get; }

    /// <summary>Matches the [Reference] tag on the logged line.</summary>
    public string Reference { get; }
}
