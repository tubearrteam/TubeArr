namespace TubeArr.Backend.Data;

public sealed class NamingConfigEntity
{
	public int Id { get; set; } = 1;

	public bool RenameVideos { get; set; } = true;
	public bool ReplaceIllegalCharacters { get; set; } = true;

	public int ColonReplacementFormat { get; set; } = 0;
	public string CustomColonReplacementFormat { get; set; } = "";

	public int MultiVideoStyle { get; set; } = 0;

	public string StandardVideoFormat { get; set; } = "{Upload Date} - {Video Title} [{Video Id}]";
	public string DailyVideoFormat { get; set; } = "{Upload Date} - {Video Title}";
	public string EpisodicVideoFormat { get; set; } = "{Upload Date} - {Video Title}";
	public string StreamingVideoFormat { get; set; } = "{Upload Date} - {Video Title}";

	public string ChannelFolderFormat { get; set; } = "{Channel Name}";
	public string PlaylistFolderFormat { get; set; } = "{Playlist Title}";
	public string SpecialsFolderFormat { get; set; } = "{Channel Name}";
}

