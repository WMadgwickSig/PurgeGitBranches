namespace GithubAction.Models;

public class CommitCommitModel
{
    public AuthorModel Author { get; set; } = null!;
    public AuthorModel? Committer { get; set; }
    public string? Message { get; set; }
    public string? Url { get; set; }
}
