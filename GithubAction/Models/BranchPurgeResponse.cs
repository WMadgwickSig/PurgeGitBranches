namespace GithubAction.Models;

public class BranchPurgeResponse
{
    public string Branch { get; set; }
    public string BranchAge { get; set; }
    public bool Deleted { get; set; }
    public string Message { get; set; }
}
