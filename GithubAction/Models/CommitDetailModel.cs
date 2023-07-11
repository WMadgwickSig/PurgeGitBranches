namespace GithubAction.Models;

public class CommitDetailModel
{
    public string Sha { get; set; }
    public string NodeId { get; set; }
    public CommitCommitModel Commit { get; set; }
}
