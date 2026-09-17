namespace Applr.Services.DTOs;

/// <summary>
/// The single shape every failed Applr.API request returns, written by
/// Applr.API's ExceptionHandlingMiddleware and read back by
/// ApplrClient. Applr.RestApi's own ApiErrorResponse is the same three
/// properties, so a failure that starts in the REST API can be read by
/// the same code path.
///
/// Message is the only part meant for a user. Nothing here ever carries
/// an exception message, a stack trace, a connection string or a
/// hostname -- those stay in the log line that Reference points at.
/// </summary>
public sealed class ApiErrorResponse
{
    public string Message { get; init; } = string.Empty;

    /// <summary>Matches the [Reference] tag on the logged line.</summary>
    public string Reference { get; init; } = string.Empty;

    public int Status { get; init; }
}
