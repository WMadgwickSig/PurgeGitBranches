namespace GithubAction.Models;

public class BranchModel
{
    public string Name { get; set; } = null!;
    public CommitModel? Commit { get; set; }
    public bool Protected { get; set; }
}
