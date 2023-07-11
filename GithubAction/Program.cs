using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using GithubAction.Models;
using System.Collections.Generic;
using Newtonsoft.Json;
using static CommandLine.Parser;

using IHost host = Host.CreateDefaultBuilder(args)
   .Build();

static TService Get<TService>(IHost host)
    where TService : notnull =>
    host.Services.GetRequiredService<TService>();

static async Task PurgeBranchesAsync(ActionInputs inputs, IHost host) 
{
    string baseUri = "https://api.github.com";
    string repo = "WMadgwickSig/PurgeGitBranches"; // TODO: get from environment variable
    var now = DateTime.UtcNow;
    List<string> branchesToExclude = new() { "master", "main", "victrix", "rapax", "herculia", "ReportBuilder" };
    
    if (!string.IsNullOrWhiteSpace(inputs.BranchedToExclude)) 
    {
        branchesToExclude.AddRange(inputs.BranchedToExclude.Split(','));
    }

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

        var pulls = await GetOpenPullRequests(client, repo);

        var branchesResponse = await client.GetAsync(branchUrl);
        var branchesStringResult = await branchesResponse.Content.ReadAsStringAsync(); // TODO: Check null or empty

        var branches = JsonConvert.DeserializeObject<IList<BranchModel>>(branchesStringResult);
        var finalResponse = new List<BranchPurgeResponse>();

        foreach(var branch in branches) 
        {
            var branchResponse = await client.GetAsync($"{branchUrl}/{branch.Name}");
            var branchStringResult = await branchResponse.Content.ReadAsStringAsync(); // TODO: Check null or empty

            var branchDetail = JsonConvert.DeserializeObject<BranchDetailModel>(branchStringResult);

            finalResponse.Add(await PurgeBranch(inputs, client, branchDetail, repo, pulls, now, branchesToExclude));
        }
    }
    catch (Exception ex)
    {
        // TODO
    }

    client.Dispose();
    Environment.Exit(0);
}

static async Task<IList<PullRequestModel>> GetOpenPullRequests(HttpClient client, string repo) 
{
    var response = await client.GetAsync($"repos/{repo}/pulls?state=open");
    var stringResult = await response.Content.ReadAsStringAsync();
    var pulls = JsonConvert.DeserializeObject<IList<PullRequestModel>>(stringResult);
    return pulls;
}

static async Task<BranchPurgeResponse> PurgeBranch(ActionInputs inputs, HttpClient client, BranchDetailModel branch, string repo, IList<PullRequestModel> pulls, DateTime now, List<string> branchesToExclude) 
{
    var branchLastActivityDate = branch.Commit.Commit.Author.Date;
    int branchLastActivityInDays = (int)(now - branchLastActivityDate).TotalDays;

    var response = new BranchPurgeResponse
    {
        BranchAge = $"{branchLastActivityInDays} days",
        Branch = branch.Name
    };

    // See if the branch is protected
    if (branch.Protected) 
    {
        response.Deleted = false;
        response.Message = "Branch is protected";

        return response;
    }

    // See if the branch should be excluded
    bool excluded = branchesToExclude.Any(a => a.Equals(branch.Name, StringComparison.OrdinalIgnoreCase));
    if (excluded) 
    {
        response.Deleted = false;
        response.Message = "Branch is excluded";

        return response;
    }

    // See if the branch has an open pull request
    bool hasOpenPull = pulls.Any(a => a.Head.Ref.Equals(branch.Name, StringComparison.OrdinalIgnoreCase) || a.Base.Ref.Equals(branch.Name, StringComparison.OrdinalIgnoreCase));
    if (hasOpenPull) 
    {
        response.Deleted = false;
        response.Message = "Branch is has an open pull request";

        return response;
    }

    // See if the branch meets the minimum last activity requirement
    if (branchLastActivityInDays < inputs.MinimumDaysSinceLastCommit)
    {
        response.Deleted = false;
        response.Message = "Branch has recent activity";

        return response;
    }

    // See if this is a dry run
    if (inputs.DryRun) 
    {
        response.Deleted = true;
        response.Message = "Deleted (Dry-run)";

        return response;
    }
    else 
    {
        try
        {
            var deleteResponse = await client.DeleteAsync($"repos/{repo}/git/refs/heads/{branch.Name}");
            if (deleteResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                response.Deleted = true;
                response.Message = "Deleted";
            }
            else
            {
                response.Deleted = false;
                response.Message = $"Unsuccessful status code: {deleteResponse.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            response.Deleted = false;
            response.Message = $"Exception thrown: {ex.Message}";
        }

        return response;
    }
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
