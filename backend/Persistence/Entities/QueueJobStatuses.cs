namespace TubeArr.Backend.Data;

/// <summary>Shared lifecycle values for <see cref="CommandQueueJobEntity"/> and <see cref="DownloadQueueEntity"/>.</summary>
public static class QueueJobStatuses
{
	public const string Queued = "queued";
	public const string Running = "running";
	public const string Completed = "completed";
	public const string Failed = "failed";
	/// <summary>Job removed before completion (e.g. user cancel).</summary>
	public const string Aborted = "aborted";
}
