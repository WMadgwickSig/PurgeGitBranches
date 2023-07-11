using GithubAction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using static CommandLine.Parser;
using System.Net.Http;
using GithubAction.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

using IHost host = Host.CreateDefaultBuilder(args)
   .Build();

static TService Get<TService>(IHost host)
    where TService : notnull =>
    host.Services.GetRequiredService<TService>();

static async Task PurgeBranchesAsync(ActionInputs inputs, IHost host) 
{
    string baseUri = "https://api.github.com";
    string repo = "WMadgwickSig/PurgeGitBranches"; // TODO: get from environment variable

    HttpClient client = new()
    {
        BaseAddress = new Uri(baseUri)
    };

    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {inputs.RepoToken}");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.Add("User-Agent", "request");

    try
    {
        string branchUrl = $"repos/{repo}/branches";

        var branchesResponse = await client.GetAsync(branchUrl);
        var branchesStringResult = await branchesResponse.Content.ReadAsStringAsync(); // TODO: Check null or empty

        var branches = JsonConvert.DeserializeObject<IList<BranchModel>>(branchesStringResult);

        foreach(var branch in branches) 
        {
            var branchResponse = await client.GetAsync($"{branchUrl}/{branch.Name}");
            var branchStringResult = await branchResponse.Content.ReadAsStringAsync(); // TODO: Check null or empty

            var branchDetail = JsonConvert.DeserializeObject<BranchDetailModel>(branchStringResult);
        }
    }
    catch (Exception ex)
    {
        // TODO
    }

    client.Dispose();
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
