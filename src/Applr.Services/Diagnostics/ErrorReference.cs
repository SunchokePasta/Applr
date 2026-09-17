namespace Applr.Services.Diagnostics;

/// <summary>
/// Short, human-quotable id attached to a single failure. The same value
/// goes into the log line, into the API's error response, and onto the
/// screen -- so a user reading "Reference: 7f3a9c21" off the front end
/// is quoting the exact log line that has the stack trace.
///
/// Lives in Applr.Services because all three tiers need it: Applr.API
/// mints one in its exception middleware, ApplrClient mints one when the
/// API can't be reached at all (no response body to read a reference
/// out of), and the desktop app mints one for its own local failures.
///
/// Applr.RestApi has its own copy of this -- the two solutions aren't
/// project-linked, same as PromoteNewJobsRequest.
/// </summary>
public static class ErrorReference
{
    public static string New() => Guid.NewGuid().ToString("N")[..8];
}
