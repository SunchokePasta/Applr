namespace Applr.Services.DTOs;

/// <summary>
/// 1:1 mirror of Applr.RestApi's `Job` entity / the `jobs` table --
/// what GET jobs/existing and GET jobs/new both deserialize into.
/// JobTitle/JobUrl/CompanyName are denormalized onto `jobs` at sync time
/// (see JobSyncService.SyncNewJobsAsync), so this DTO no longer needs a
/// join back to raw job/company details to display a row.
/// </summary>
public sealed class JobDto
{
    public uint Id { get; set; }
    public uint RawJobId { get; set; }
    public uint CompanyId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string JobUrl { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? PostedDate { get; set; }
    public DateOnly? CloseDate { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdated { get; set; }
}
