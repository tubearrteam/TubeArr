namespace TubeArr.Backend.Data;

/// <summary>FFmpeg tool settings. The application uses a single logical row with <c>Id = 1</c> (see API get-or-create).</summary>
public sealed class FFmpegConfigEntity
{
	public int Id { get; set; } = 1;

	public string ExecutablePath { get; set; } = "";
	public bool Enabled { get; set; } = true;
}
