using Applr.Services.DTOs;

namespace Applr.Services.Interfaces;

public interface ITrackrService
{
    /// <summary>jobs where status == "Unreviewed".</summary>
    Task<IReadOnlyList<JobDto>> GetExistingJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>jobs where status == "New".</summary>
    Task<IReadOnlyList<JobDto>> GetNewJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>Flips the given job ids from 'New' to 'Unreviewed'.</summary>
    Task PromoteNewJobsAsync(IReadOnlyList<uint> jobIds, CancellationToken cancellationToken = default);
}
