using JobFinder.Services.DTOs;
using Refit;

namespace JobFinder.Services.APIs;

public interface ITrackrRestApi
{
    [Get("/jobs")]
    Task<List<DbJobDto>> GetAllJobsAsync(
        CancellationToken cancellationToken = default);
}
