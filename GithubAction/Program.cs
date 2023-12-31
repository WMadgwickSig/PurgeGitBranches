﻿using Microsoft.Extensions.DependencyInjection;
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
using System.IO;
using System.Text;
using static CommandLine.Parser;

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
        bool isDryRun = BoolValue(inputs.DryRun);
        bool wasMerged = BoolValue(inputs.WasMerged);

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

        var branches = new List<BranchModel>();

        await GetBranches(client, repo, branches);
        var pulls = await GetOpenPullRequests(client, repo);

        var finalResponse = new List<BranchPurgeResponse>();

        foreach(var branch in branches) 
        {
            var branchDetail = await GetBranchDetail(branch.Name, client, repo);

            finalResponse.Add(await PurgeBranch(inputs, client, branchDetail, repo, pulls, now, branchesToExclude, isDryRun, wasMerged));
        }

        if (!string.IsNullOrWhiteSpace(gitHubOutputFile)) 
        {
            using StreamWriter textWriter = new(gitHubOutputFile, true, Encoding.UTF8);
            textWriter.WriteLine($"was-dryrun={inputs.DryRun}");
            textWriter.WriteLine($"was-merged={inputs.WasMerged}");
            textWriter.WriteLine($"min-days-since-last-commit={inputs.MinimumDaysSinceLastCommit}");
            textWriter.WriteLine($"excluded-branches={string.Join(',', branchesToExclude)}");
            textWriter.WriteLine($"total-branches-purged={finalResponse.Where(w => w.Purged).Count()}");
            textWriter.WriteLine($"result-json={JsonConvert.SerializeObject(finalResponse)}");
        }
        else 
        {
            Console.WriteLine($"was-dryrun={inputs.DryRun}");
            Console.WriteLine($"was-merged={inputs.WasMerged}");
            Console.WriteLine($"min-days-since-last-commit={inputs.MinimumDaysSinceLastCommit}");
            Console.WriteLine($"excluded-branches={string.Join(',', branchesToExclude)}");
            Console.WriteLine($"total-branches-purged={finalResponse.Where(w => w.Purged).Count()}");
            Console.WriteLine($"result-json={JsonConvert.SerializeObject(finalResponse)}");
        }
    }
    catch (Exception ex)
    {
        if (!string.IsNullOrWhiteSpace(gitHubOutputFile))
        {
            using StreamWriter textWriter = new(gitHubOutputFile, true, Encoding.UTF8);
            textWriter.WriteLine($"exception={ex.Message}|{ex.StackTrace}");
        }
        else 
        {
            Console.WriteLine($"exception={ex.Message}|{ex.StackTrace}");
        }
    }

    client.Dispose();
    Environment.Exit(0);
}

static bool BoolValue(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) 
    {
        return false;
    }

    if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) 
    {
        return true;
    }

    if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    throw new Exception($"Could not get bool value {value}");
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

static async Task GetBranches(HttpClient client, string repo, List<BranchModel> branches, int pageIndex = 1) 
{
    string branchUrl = $"repos/{repo}/branches?per_page=100&page={pageIndex}";
    var response = await client.GetAsync(branchUrl);
    var stringResult = await response.Content.ReadAsStringAsync();
    var fetchedBranches = JsonConvert.DeserializeObject<List<BranchModel>>(stringResult) ?? new List<BranchModel>();
    branches.AddRange(fetchedBranches);

    if (fetchedBranches.Count == 0) 
    {
        return;
    }

    pageIndex++;
    await GetBranches(client, repo, branches, pageIndex);
}

static async Task<IList<PullRequestModel>> GetOpenPullRequests(HttpClient client, string repo) 
{
    var response = await client.GetAsync($"repos/{repo}/pulls?state=all");
    var stringResult = await response.Content.ReadAsStringAsync();
    var pulls = JsonConvert.DeserializeObject<IList<PullRequestModel>>(stringResult) ?? new List<PullRequestModel>();
    return pulls;
}

static async Task<BranchPurgeResponse> PurgeBranch(ActionInputs inputs, HttpClient client, BranchDetailModel branch, string repo, IList<PullRequestModel> pulls, DateTime now, List<string> branchesToExclude, bool isDryRun, bool wasMerged) 
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
        response.Purged = false;
        response.Message = "Branch is protected";

        return response;
    }

    // See if the branch should be excluded
    bool excluded = branchesToExclude.Any(a => a.Equals(branch.Name, StringComparison.OrdinalIgnoreCase));
    if (excluded) 
    {
        response.Purged = false;
        response.Message = "Branch is excluded";

        return response;
    }

    // See if the branch has an open pull request
    bool hasOpenPull = pulls.Any(a => a.State == "open" && (a.Head.Ref.Equals(branch.Name, StringComparison.OrdinalIgnoreCase) || a.Base.Ref.Equals(branch.Name, StringComparison.OrdinalIgnoreCase)));
    if (hasOpenPull) 
    {
        response.Purged = false;
        response.Message = "Branch has an open pull request";

        return response;
    }

    // See if the branch meets the minimum last activity requirement
    if (branchLastActivityInDays < inputs.MinimumDaysSinceLastCommit)
    {
        response.Purged = false;
        response.Message = "Branch has recent activity";

        return response;
    }

    // If this flag is on, only delete branched which had a PR and has been merged in
    if (wasMerged)
    {
        bool branchWasMergedIn = pulls.Any(a => a.MergedAt.HasValue && a.Head.Ref.Equals(branch.Name, StringComparison.OrdinalIgnoreCase));
        if (!branchWasMergedIn)
        {
            response.Purged = false;
            response.Message = "Branch has not been merged in.";

            return response;
        }
    }

    // See if this is a dry run
    if (isDryRun)
    {
        response.Purged = true;
        response.Message = "Purged (Dry-run)";

        return response;
    }

    try
    {
        var deleteResponse = await client.DeleteAsync($"repos/{repo}/git/refs/heads/{branch.Name}");
        if (deleteResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            response.Purged = true;
            response.Message = "Purged";
        }
        else
        {
            response.Purged = false;
            response.Message = $"Unsuccessful status code: {deleteResponse.StatusCode}";
        }
    }
    catch (Exception ex)
    {
        response.Purged = false;
        response.Message = $"Exception thrown: {ex.Message}";
    }

    return response;
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
