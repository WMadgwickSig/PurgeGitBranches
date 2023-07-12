namespace GithubAction.Models;

public class BranchPurgeResponse
{
    public string Branch { get; set; } = null!;
    public string BranchAge { get; set; } = null!;
    public bool Deleted { get; set; }
    public string? Message { get; set; }
}
