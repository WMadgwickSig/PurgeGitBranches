using CommandLine;

namespace GithubAction.Models;

public class ActionInputs
{
    [Option('t', "repoToken", Required = true, HelpText = "Github repository token used for authentication.")]
    public string RepoToken { get; set; } = null!;

    [Option('d', "dryRun", Required = true, HelpText = "Do a dry or test run first to see which branches will be purged?")]
    public string DryRun { get; set; } = null!;

    [Option('c', "daysSinceLastCommit", Required = true, HelpText = "Minimum days since last commit?")]
    public int MinimumDaysSinceLastCommit { get; set; }

    [Option('e', "branchesToExclude", Required = false, HelpText = "Branches to exclude as a comma separated list", Default = "main,master,develop")]
    public string BranchedToExclude { get; set; } = null!;
}
