namespace JobFinder.Services.DTOs;

public sealed class ArbeitNowJobDto
{
    public string Slug { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Remote { get; set; }

    public string Url { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public List<string> JobTypes { get; set; } = [];

    public string Location { get; set; } = string.Empty;

    public long CreatedAt { get; set; }
}
