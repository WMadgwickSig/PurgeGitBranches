namespace GithubAction.Models;

public class BranchDeleteResponse
{
    public string Branch { get; set; }
    public string BranchAge { get; set; }
    public bool Deleted { get; set; }
    public string Reason { get; set; }
}
