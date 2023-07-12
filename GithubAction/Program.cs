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
using System.IO;
using System.Text;

using IHost host = Host.CreateDefaultBuilder(args)
   .Build();

static TService Get<TService>(IHost host)
    where TService : notnull =>
    host.Services.GetRequiredService<TService>();

static async Task PurgeBranchesAsync(ActionInputs inputs, IHost host) 
{
    var gitHubOutputFile = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    string baseUri = "https://api.github.com";

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
        string? repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");

        if (string.IsNullOrWhiteSpace(repo))
        {
            throw new Exception("Could not find repo from the GITHUB_REPOSITORY env variable");
        }

        var now = DateTime.UtcNow;
        List<string> branchesToExclude = new();

        if (!string.IsNullOrWhiteSpace(inputs.BranchedToExclude))
        {
            branchesToExclude.AddRange(inputs.BranchedToExclude.Split(','));
        }

        var branches = await GetBranches(client, repo);
        var pulls = await GetOpenPullRequests(client, repo);

        var finalResponse = new List<BranchPurgeResponse>();

        foreach(var branch in branches) 
        {
            var branchDetail = await GetBranchDetail(branch.Name, client, repo);

            finalResponse.Add(await PurgeBranch(inputs, client, branchDetail, repo, pulls, now, branchesToExclude));
        }

        if (!string.IsNullOrWhiteSpace(gitHubOutputFile)) 
        {
            using StreamWriter textWriter = new(gitHubOutputFile, true, Encoding.UTF8);
            textWriter.WriteLine($"was-dryrun={inputs.DryRun}");
            textWriter.WriteLine($"min-days-since-last-commit={inputs.MinimumDaysSinceLastCommit}");
            textWriter.WriteLine($"excluded-branches={string.Join(',', branchesToExclude)}");
            textWriter.WriteLine($"branches-purged={finalResponse.Where(w => w.Deleted).Count()}");
            textWriter.WriteLine($"result-json={JsonConvert.SerializeObject(finalResponse)}");
        }
        else 
        {
            Console.WriteLine($"was-dryrun={inputs.DryRun}");
            Console.WriteLine($"min-days-since-last-commit={inputs.MinimumDaysSinceLastCommit}");
            Console.WriteLine($"excluded-branches={string.Join(',', branchesToExclude)}");
            Console.WriteLine($"branches-purged={finalResponse.Where(w => w.Deleted).Count()}");
            Console.WriteLine($"result-json={JsonConvert.SerializeObject(finalResponse)}");
        }
    }
    catch (Exception ex)
    {
        if (!string.IsNullOrWhiteSpace(gitHubOutputFile))
        {
            using StreamWriter textWriter = new(gitHubOutputFile, true, Encoding.UTF8);
            textWriter.WriteLine($"exception={ex.Message}");
        }
        else 
        {
            Console.WriteLine($"exception={ex.Message}");
        }
    }

    client.Dispose();
    Environment.Exit(0);
}

static async Task<BranchDetailModel> GetBranchDetail(string branch, HttpClient client, string repo) 
{
    string branchUrl = $"repos/{repo}/branches";
    var branchResponse = await client.GetAsync($"{branchUrl}/{branch}");
    var branchStringResult = await branchResponse.Content.ReadAsStringAsync();
    var branchDetail = JsonConvert.DeserializeObject<BranchDetailModel>(branchStringResult) 
        ?? throw new Exception($"Could not get branch detail for {branch}");

    return branchDetail;
}

static async Task<IList<BranchModel>> GetBranches(HttpClient client, string repo) 
{
    string branchUrl = $"repos/{repo}/branches";
    var response = await client.GetAsync(branchUrl);
    var stringResult = await response.Content.ReadAsStringAsync();
    var branches = JsonConvert.DeserializeObject<IList<BranchModel>>(stringResult) ?? new List<BranchModel>();
    return branches;
}

static async Task<IList<PullRequestModel>> GetOpenPullRequests(HttpClient client, string repo) 
{
    var response = await client.GetAsync($"repos/{repo}/pulls?state=open");
    var stringResult = await response.Content.ReadAsStringAsync();
    var pulls = JsonConvert.DeserializeObject<IList<PullRequestModel>>(stringResult) ?? new List<PullRequestModel>();
    return pulls;
}

static async Task<BranchPurgeResponse> PurgeBranch(ActionInputs inputs, HttpClient client, BranchDetailModel branch, string repo, IList<PullRequestModel> pulls, DateTime now, List<string> branchesToExclude) 
{
    var branchLastActivityDate = branch.Commit.Commit.Author.Date;
    int branchLastActivityInDays = (int)(now - branchLastActivityDate).TotalDays;

    var response = new BranchPurgeResponse
    {
        DaysSinceLastActivity = $"{branchLastActivityInDays} days",
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
