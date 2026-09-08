using JobFinder.Services.DTOs;

namespace JobFinder.Services.Interfaces;

public interface IArbeitNowService
{
    Task<ArbeitNowResponseDto> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
