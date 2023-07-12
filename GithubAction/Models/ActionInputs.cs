using CommandLine;
using System;

namespace GithubAction.Models;

public class ActionInputs
{
    [Option('t', "repoToken", Required = true, HelpText = "Github repository token used for authentication.")]
    public string RepoToken { get; set; } = null!;

    [Option('d', "dryRun", Required = true, HelpText = "Do a dry or test run first to see which branches will be purged?")]
    public bool DryRun { get; set; }

    [Option('c', "daysSinceLastCommit", Required = true, HelpText = "Minimum days since last commit?")]
    public int MinimumDaysSinceLastCommit { get; set; }

    [Option('e', "branchesToExclude", Required = false, HelpText = "Branches to exclude as a comma separated list", Default = "main,master,develop")]
    public string BranchedToExclude { get; set; } = null!;

    static void ParseAndAssign(string? value, Action<string> assign)
    {
        if (value is { Length: > 0 } && assign is not null)
        {
            assign(value.Split("/")[^1]);
        }
    }
}
