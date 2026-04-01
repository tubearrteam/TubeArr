namespace TubeArr.Backend.Data;

public sealed class ChannelCustomPlaylistEntity
{
	public int Id { get; set; }
	public int ChannelId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
	public int Priority { get; set; }
	/// <summary>0 = All rules must match, 1 = Any rule matches (<see cref="ChannelCustomPlaylistMatchType"/>).</summary>
	public int MatchType { get; set; }
	public string RulesJson { get; set; } = "[]";
}
