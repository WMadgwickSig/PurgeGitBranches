using CommandLine;
using System;

namespace GithubAction;

public class ActionInputs
{
    [Option('t', "repoToken", Required = true, HelpText = "Github repository token used for authentication.")]
    public string RepoToken { get; set; } = null!;

    [Option('d', "dryRun", Required = true, HelpText = "Do a dry or test run first to see which branches will be purged?")]
    public bool DryRun { get; set; }

    static void ParseAndAssign(string? value, Action<string> assign)
    {
        if (value is { Length: > 0 } && assign is not null)
        {
            assign(value.Split("/")[^1]);
        }
    }
}
