using JobFinder.Services.DTOs;
using Refit;

namespace JobFinder.Services.APIs;

public interface IArbeitNowApi
{
    [Get("")]
    Task<ArbeitNowResponseDto> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
