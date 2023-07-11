namespace GithubAction.Models;

public class CommitCommitModel
{
    public AuthorModel Author { get; set; }
    public AuthorModel Committer { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
}
