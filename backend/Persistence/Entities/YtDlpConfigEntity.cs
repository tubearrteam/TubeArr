namespace TubeArr.Backend.Data;

public sealed class YtDlpConfigEntity
{
	public int Id { get; set; } = 1;

	public string ExecutablePath { get; set; } = "";
	public bool Enabled { get; set; } = true;
}
