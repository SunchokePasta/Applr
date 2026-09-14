using Applr.Services.DTOs;
using Refit;

namespace Applr.Services.APIs;

public interface ITrackrRestApi
{
    [Get("/jobs")]
    Task<List<DbJobDto>> GetAllJobsAsync(
        CancellationToken cancellationToken = default);
}
