namespace TubeArr.Backend.Data;

/// <summary>Pipeline grouping for staged command jobs (mirrors former Metadata/FileOps/DbOps queue split).</summary>
public static class CommandQueueJobCategories
{
	public const string Metadata = "metadata";
	public const string FileOps = "fileops";
	public const string DbOps = "dbops";
}
