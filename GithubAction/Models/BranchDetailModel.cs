namespace GithubAction.Models;

public class BranchDetailModel
{
    public string Name { get; set; }
    public CommitDetailModel Commit { get; set; }
    public bool Protected { get; set; }
}
