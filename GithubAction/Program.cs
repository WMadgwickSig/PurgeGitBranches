using GithubAction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using System;
using static CommandLine.Parser;
using System.Linq;

using IHost host = Host.CreateDefaultBuilder(args)
   .Build();

static TService Get<TService>(IHost host)
    where TService : notnull =>
    host.Services.GetRequiredService<TService>();

static async Task PurgeBranchesAsync(ActionInputs inputs, IHost host) 
{
    Environment.Exit(0);
}

var parser = Default.ParseArguments<ActionInputs>(() => new(), args);
parser.WithNotParsed(
    errors =>
    {
        Get<ILoggerFactory>(host)
            .CreateLogger("PurgeGitBranches.Program")
            .LogError(
                string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        Environment.Exit(2);
    });

await parser.WithParsedAsync(options => PurgeBranchesAsync(options, host));
await host.RunAsync();
