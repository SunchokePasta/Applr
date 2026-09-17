namespace Applr.Services.DTOs;

/// <summary>
/// Mirrors Applr.RestApi's own PromoteNewJobsRequest -- the two
/// solutions aren't project-linked, so this is kept in sync by hand.
/// </summary>
public sealed class PromoteNewJobsRequest
{
    public List<uint> JobIds { get; set; } = [];
}
