namespace JobFinder.DesktopServices.Interfaces;

public interface IJobFinderClient
{
    Task<string> GetJobsAsync(
        CancellationToken cancellationToken = default);
}
