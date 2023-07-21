using Newtonsoft.Json;
using System;

namespace GithubAction.Models;

public class PullRequestModel
{
    public int Id { get; set; }
    // Open/Closed/All
    public string State { get; set; } = null!;
    [JsonProperty("merged_at")]
    public DateTime? MergedAt { get; set; }
    public PullRequestDetailModel Head { get; set; } = null!;
    public PullRequestDetailModel Base { get; set; } = null!;
}
