using System.Text.Json.Serialization;

namespace TubeArr.Backend;

/// <summary>Single rule row stored in <see cref="Data.ChannelCustomPlaylistEntity.RulesJson"/> (max 5 per playlist).</summary>
public sealed class ChannelCustomPlaylistRule
{
	[JsonPropertyName("field")]
	public string Field { get; set; } = string.Empty;

	[JsonPropertyName("operator")]
	public string Operator { get; set; } = string.Empty;

	[JsonPropertyName("value")]
	public System.Text.Json.JsonElement? Value { get; set; }
}
