using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

/// <summary>Command name routing entry point; implementation bodies live in <see cref="CommandDispatcher"/> partials.</summary>
public sealed partial class CommandDispatcher
{
	public async Task<Dictionary<string, object?>> DispatchAsync(
		JsonElement payload,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime,
		ChannelMetadataAcquisitionService channelMetadataAcquisitionService,
		YouTubeDataApiMetadataService youTubeDataApiMetadataService)
	{
		var name = payload.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
			? nameEl.GetString() ?? ""
			: "";

		var trigger = payload.TryGetProperty("trigger", out var triggerEl) && triggerEl.ValueKind == JsonValueKind.String
			? triggerEl.GetString() ?? "manual"
			: "manual";

		if (string.Equals(name, "RefreshChannels", StringComparison.OrdinalIgnoreCase))
		{
			var ids = await db.Channels.AsNoTracking()
				.Where(c => c.Monitored)
				.Select(c => c.Id)
				.ToListAsync();
			if (ids.Count == 0)
				return await CreateFailedScheduledTaskCommandAsync(
					name,
					trigger,
					"No monitored channels to refresh.",
					realtime);

			return await QueueRefreshChannelsAsync(ids, name, trigger, realtime);
		}

		if (string.Equals(name, "RefreshMonitoredDownloads", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareRefreshMonitoredDownloadsAsync(name, trigger, scopeFactory, logger, realtime);

		if (string.Equals(name, "RefreshChannel", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareRefreshChannelAsync(payload, name, trigger, realtime);

		if (string.Equals(name, "GetVideoDetails", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareGetVideoDetailsAsync(payload, name, trigger, db, scopeFactory, logger, realtime, channelMetadataAcquisitionService);

		if (string.Equals(name, "GetChannelPlaylists", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareGetChannelPlaylistsAsync(payload, name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "RssSync", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareRssSyncAsync(payload, name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "ResetApiKey", StringComparison.OrdinalIgnoreCase))
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			serverSettings.ApiKey = ProgramStartupHelpers.GenerateApiKey();
			await db.SaveChangesAsync();
			using var invScope = scopeFactory.CreateScope();
			invScope.ServiceProvider.GetRequiredService<ApiSecuritySettingsCache>().Invalidate();
		}

		if (string.Equals(name, "CleanUpRecycleBin", StringComparison.OrdinalIgnoreCase))
			return await HandleCleanUpRecycleBinCommandAsync(name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "Housekeeping", StringComparison.OrdinalIgnoreCase))
			return await HandleHousekeepingCommandAsync(name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "MapUnmappedVideoFiles", StringComparison.OrdinalIgnoreCase))
			return await HandleMapUnmappedVideoFilesCommandAsync(name, trigger, db, logger, realtime);

		if (string.Equals(name, "SyncCustomNfos", StringComparison.OrdinalIgnoreCase))
			return await HandleSyncCustomNfosCommandAsync(name, trigger, db, logger, realtime);

		if (string.Equals(name, "RepairLibraryNfosAndArtwork", StringComparison.OrdinalIgnoreCase))
			return await HandleRepairLibraryNfosAndArtworkCommandAsync(name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "RenameFiles", StringComparison.OrdinalIgnoreCase))
			return await HandleRenameFilesCommandAsync(payload, name, trigger, db, logger, realtime);

		if (string.Equals(name, "RenameChannel", StringComparison.OrdinalIgnoreCase))
			return await HandleRenameChannelCommandAsync(payload, name, trigger, db, logger, realtime);

		if (string.Equals(name, "CheckHealth", StringComparison.OrdinalIgnoreCase))
			return await HandleCheckHealthCommandAsync(name, trigger, db, realtime, youTubeDataApiMetadataService);

		if (string.Equals(name, "ApplicationUpdate", StringComparison.OrdinalIgnoreCase))
			return await HandleApplicationUpdateCommandAsync(name, trigger, db, scopeFactory, realtime);

		if (string.Equals(name, "MessagingCleanup", StringComparison.OrdinalIgnoreCase))
			return await HandleMessagingCleanupCommandAsync(name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "Backup", StringComparison.OrdinalIgnoreCase))
			return await HandleBackupCommandAsync(name, trigger, scopeFactory, realtime);

		return await HandleLegacyCommandAsync(payload, name, trigger, db, scopeFactory, logger, realtime, channelMetadataAcquisitionService);
	}
}
