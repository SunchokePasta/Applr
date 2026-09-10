using JobFinder.Services.DTOs;

namespace JobFinder.DesktopServices.Interfaces;

public interface IJobFinderClient
{
    Task<IReadOnlyList<ScrapedJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
