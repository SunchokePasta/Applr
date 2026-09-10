namespace JobFinder.Services.DTOs;

public sealed class ScrapedJobDto
{
    // Trackr status
    public string? Status { get; set; }

    // Company
    public string? CompanyName { get; set; }
    public string? CompanyUrl { get; set; }

    // Job
    public string? JobTitle { get; set; }
    public string? JobUrl { get; set; }

    // Dates
    public string? CloseDate { get; set; }
    public string? PostedDate { get; set; }

    // Requirements / application information
    public bool CvRequired { get; set; }

    // Sponsorship
    public bool VisaSponsorship { get; set; }

    // Any other text/content from the row that we haven't
    // explicitly mapped yet.
    public List<string> RawCells { get; set; } = [];
}