namespace GithubAction.Models;

public class PullRequestModel
{
    public int Id { get; set; }
    public PullRequestDetailModel Head { get; set; } = null!;
    public PullRequestDetailModel Base { get; set; } = null!;
}
