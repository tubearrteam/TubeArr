using System;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class CommandQueueJobCategoryResolver
{
	public static string? FromJobType(string? jobType)
	{
		if (string.IsNullOrEmpty(jobType))
			return null;

		if (IsMetadataJobType(jobType))
			return CommandQueueJobCategories.Metadata;
		if (IsFileOpsJobType(jobType))
			return CommandQueueJobCategories.FileOps;
		if (IsDbOpsJobType(jobType))
			return CommandQueueJobCategories.DbOps;
		return null;
	}

	public static bool IsMetadataJobType(string jobType) =>
		string.Equals(jobType, CommandQueueJobTypes.RefreshChannel, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.GetVideoDetails, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.GetChannelPlaylists, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.RssSync, StringComparison.OrdinalIgnoreCase);

	public static bool IsFileOpsJobType(string jobType) =>
		string.Equals(jobType, CommandQueueJobTypes.RenameFiles, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.RenameChannel, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.MapUnmappedVideoFiles, StringComparison.OrdinalIgnoreCase);

	public static bool IsDbOpsJobType(string jobType) =>
		string.Equals(jobType, CommandQueueJobTypes.SyncCustomNfos, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.RepairLibraryNfosAndArtwork, StringComparison.OrdinalIgnoreCase);
}
