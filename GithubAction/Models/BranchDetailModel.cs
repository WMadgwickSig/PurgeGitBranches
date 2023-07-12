namespace GithubAction.Models;

public class BranchDetailModel
{
    public string Name { get; set; } = null!;
    public CommitDetailModel Commit { get; set; } = null!;
    public bool Protected { get; set; }
}
