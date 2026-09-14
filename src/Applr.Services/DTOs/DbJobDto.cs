namespace Applr.Services.DTOs;

/// <summary>
/// 1:1 mirror of Applr.RestApi's `Job` entity / the `jobs` table --
/// every column, same shape. This is what ITrackrRestApi deserializes
/// the DB-backed API's response into, and (since ScrapedJobDto no
/// longer exists) what travels all the way up through TrackrService,
/// TrackrController, and the desktop client to the frontend.
/// </summary>
public sealed class DbJobDto
{
    public uint Id { get; set; }
    public string JobUrl { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyUrl { get; set; }
    public string? Status { get; set; }
    public DateOnly? PostedDate { get; set; }
    public string? PostedDateRaw { get; set; }
    public DateOnly? CloseDate { get; set; }
    public string? CloseDateRaw { get; set; }
    public bool CvRequired { get; set; }
    public bool CoverLetterRequired { get; set; }
    public bool WrittenAnswersRequired { get; set; }
    public bool VisaSponsorship { get; set; }
    public List<string>? RawCells { get; set; }
    public string JobIdentityHash { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdated { get; set; }
}
