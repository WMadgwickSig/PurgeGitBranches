namespace GithubAction.Models;

public class BranchPurgeResponse
{
    public string Branch { get; set; } = null!;
    public string DaysSinceLastActivity { get; set; } = null!;
    public bool Purged { get; set; }
    public string? Message { get; set; }
}
