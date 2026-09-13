using JobFinder.Services.DTOs;

namespace JobFinder.DesktopServices.Interfaces;

public interface IJobFinderClient
{
    Task<IReadOnlyList<DbJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
