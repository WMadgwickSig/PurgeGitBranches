using Newtonsoft.Json;

namespace GithubAction.Models;

public class CommitDetailModel
{
    public string Sha { get; set; }
    [JsonProperty("node_id")]
    public string NodeId { get; set; }
    public CommitCommitModel Commit { get; set; }
}
