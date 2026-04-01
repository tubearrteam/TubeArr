using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media.Nfo;
using TubeArr.Backend.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TubeArr.Backend;

public sealed class CommandDispatcher
{
	readonly CommandRecordFactory _records;
	readonly ICommandExecutionQueue _commandQueue;
	readonly BackupRestoreService _backupRestore;
	readonly IScheduledTaskRunRecorder _scheduledTaskRunRecorder;
	readonly IServiceScopeFactory _scopeFactory;

	public CommandDispatcher(
		CommandRecordFactory records,
		ICommandExecutionQueue commandQueue,
		BackupRestoreService backupRestore,
		IScheduledTaskRunRecorder scheduledTaskRunRecorder,
		IServiceScopeFactory scopeFactory)
	{
		_records = records;
		_commandQueue = commandQueue;
		_backupRestore = backupRestore;
		_scheduledTaskRunRecorder = scheduledTaskRunRecorder;
		_scopeFactory = scopeFactory;
	}

	public Task<Dictionary<string, object?>> QueueRefreshChannelAsync(
		int channelId,
		string trigger,
		IRealtimeEventBroadcaster realtime)
	{
		return QueueRefreshChannelsAsync([channelId], "RefreshChannel", trigger, realtime);
	}

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

	async Task<Dictionary<string, object?>> HandleProgressAwareRefreshChannelAsync(
		JsonElement payload,
		string name,
		string trigger,
		IRealtimeEventBroadcaster realtime)
	{
		var channelIds = new List<int>();
		if (payload.TryGetProperty("channelId", out var channelIdEl) &&
			channelIdEl.ValueKind == JsonValueKind.Number &&
			channelIdEl.TryGetInt32(out var singleChannelId))
		{
			channelIds.Add(singleChannelId);
		}

		if (payload.TryGetProperty("channelIds", out var channelIdsEl) && channelIdsEl.ValueKind == JsonValueKind.Array)
		{
			foreach (var channelIdValue in channelIdsEl.EnumerateArray())
			{
				if (channelIdValue.ValueKind == JsonValueKind.Number && channelIdValue.TryGetInt32(out var channelId))
					channelIds.Add(channelId);
			}
		}

		channelIds = channelIds
			.Where(id => id > 0)
			.Distinct()
			.ToList();

		var initialPhase = ChannelRefreshPhase.UploadsPopulation;
		if (channelIds.Count == 1 &&
		    payload.TryGetProperty("phase", out var phaseEl) &&
		    phaseEl.ValueKind == JsonValueKind.String)
		{
			var p = phaseEl.GetString()?.Trim();
			if (string.Equals(p, ChannelRefreshPhase.Hydration, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.Hydration;
			else if (string.Equals(p, ChannelRefreshPhase.LivestreamIdentification, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.LivestreamIdentification;
			else if (string.Equals(p, ChannelRefreshPhase.ShortsParsing, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.ShortsParsing;
			else if (string.Equals(p, ChannelRefreshPhase.PlaylistDiscovery, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.PlaylistDiscovery;
			else if (string.Equals(p, ChannelRefreshPhase.PlaylistPopulation, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.PlaylistPopulation;
			else if (string.Equals(p, ChannelRefreshPhase.UploadsPopulation, StringComparison.OrdinalIgnoreCase))
				initialPhase = ChannelRefreshPhase.UploadsPopulation;
		}

		var stopAfterThisPhase = false;
		if (payload.TryGetProperty("stopAfterPhase", out var stopAfterEl) && stopAfterEl.ValueKind == JsonValueKind.True)
			stopAfterThisPhase = true;

		return await QueueRefreshChannelsAsync(channelIds, name, trigger, realtime, initialPhase, stopAfterThisPhase);
	}

	async Task<Dictionary<string, object?>> QueueRefreshChannelsAsync(
		IEnumerable<int> channelIds,
		string name,
		string trigger,
		IRealtimeEventBroadcaster realtime,
		string initialPhase = ChannelRefreshPhase.UploadsPopulation,
		bool stopAfterThisPhase = false)
	{
		var normalizedChannelIds = channelIds
			.Where(id => id > 0)
			.Distinct()
			.ToList();

		if (normalizedChannelIds.Count == 0)
		{
			var failedQueuedAt = DateTimeOffset.UtcNow;
			var failedBody = new Dictionary<string, object?>
			{
				["name"] = name,
				["trigger"] = trigger,
				["sendUpdatesToClient"] = true,
				["suppressMessages"] = false
			};

			var failedCommand = _records.CreateCommandRecord(
				name,
				trigger,
				failedBody,
				status: "failed",
				result: "unsuccessful",
				message: "channelId is required for Refresh & Scan.",
				queuedAt: failedQueuedAt,
				startedAt: failedQueuedAt,
				endedAt: failedQueuedAt);

			await _records.BroadcastCommandUpdateAsync(realtime, failedCommand);
			return _records.SnapshotCommand(failedCommand);
		}

		var allIds = normalizedChannelIds.ToArray();
		var batchStart = DateTimeOffset.UtcNow;
		var recordBatch = string.Equals(name, "RefreshChannels", StringComparison.OrdinalIgnoreCase);

		var firstPayload = new RefreshChannelQueueJobPayload(
			name,
			trigger,
			new[] { allIds[0] },
			initialPhase,
			allIds,
			0,
			recordBatch,
			batchStart,
			SerializedPlaylistDiscoveryItems: null,
			StopAfterThisPhase: stopAfterThisPhase);

		return await EnqueueRefreshChannelPhaseAsync(realtime, firstPayload, CancellationToken.None);
	}

	async Task<Dictionary<string, object?>> EnqueueRefreshChannelPhaseAsync(
		IRealtimeEventBroadcaster realtime,
		RefreshChannelQueueJobPayload payload,
		CancellationToken ct)
	{
		var phase = payload.Phase ?? ChannelRefreshPhase.UploadsPopulation;
		var phaseCommandName = GetRefreshChannelPhaseCommandName(phase);
		var queuedAt = DateTimeOffset.UtcNow;
		var body = new Dictionary<string, object?>
		{
			["name"] = phaseCommandName,
			["trigger"] = payload.Trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
			["channelId"] = payload.ChannelIds[0],
			["channelIds"] = payload.ChannelIds,
			["metadataStep"] = phase,
			["originalCommandName"] = payload.Name
		};

		var command = _records.CreateCommandRecord(
			phaseCommandName,
			payload.Trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: queuedAt,
			startedAt: queuedAt,
			endedAt: queuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, command);

		if (!TryGetCommandId(command, out var commandId))
			return _records.SnapshotCommand(command);

		var json = JsonSerializer.Serialize(payload);
		await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(
			commandId,
			phaseCommandName,
			CommandQueueJobTypes.RefreshChannel,
			json,
			ct => ExecuteRefreshChannelWorkAsync(commandId, payload, ct)), ct);

		return _records.SnapshotCommand(command);
	}

	public async Task ExecuteRefreshChannelWorkAsync(int commandId, RefreshChannelQueueJobPayload payload, CancellationToken ct)
	{
		if (!_records.TryGetCommandById(commandId, out var command))
			return;

		if (payload.Phase is null)
		{
			await ExecuteLegacyRefreshChannelAsync(command, payload, ct);
			return;
		}

		var startedAt = DateTimeOffset.UtcNow;
		var started = _records.UpdateCommandRecord(command, (resource, _) =>
		{
			resource["status"] = "started";
			resource["result"] = "unknown";
			resource["started"] = startedAt.ToString("O");
			resource["ended"] = null;
			resource["duration"] = null;
			resource["stateChangeTime"] = startedAt.ToString("O");
			resource["lastExecutionTime"] = startedAt.ToString("O");
		});

		using var scope0 = _scopeFactory.CreateScope();
		var realtime0 = scope0.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		await realtime0.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

		string? resultMessage = null;
		string? serializedPlaylistDiscoveryItems = null;
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var channelMetadataService = scope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();
			var channelId = payload.ChannelIds[0];

			async Task ReportAcquisitionMethodAsync(string methodId)
			{
				var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(commandId, methodId, ct);
				var snap = _records.UpdateCommandRecord(command, (_, body) =>
				{
					body["acquisitionMethods"] = list.ToArray();
				});
				using var s = _scopeFactory.CreateScope();
				var rt = s.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
				await rt.BroadcastAsync("command", new { action = "updated", resource = snap }, CancellationToken.None);
			}

			switch (payload.Phase)
			{
				case ChannelRefreshPhase.UploadsPopulation:
				{
					var lastPersistDetailAt = DateTimeOffset.MinValue;
					async Task NotifyPhaseDetailAsync(string detail, CancellationToken c)
					{
						var snap = _records.UpdateCommandRecord(command, (_, body) =>
						{
							body["phaseDetail"] = detail;
						});
						using var s = _scopeFactory.CreateScope();
						var rt = s.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
						await rt.BroadcastAsync("command", new { action = "updated", resource = snap }, c);
					}

					async Task NotifyPersistProgressAsync(int done, int total, CancellationToken c)
					{
						var now = DateTimeOffset.UtcNow;
						var isFinal = total > 0 && done >= total;
						if (!isFinal && (now - lastPersistDetailAt).TotalSeconds < 1.5)
							return;
						lastPersistDetailAt = now;
						await NotifyPhaseDetailAsync(
							total > 0 ? $"Saving videos to library ({done}/{total})…" : "Saving videos to library…",
							c);
					}

					resultMessage = await channelMetadataService.RunUploadsPopulationPhaseAsync(
						db,
						channelId,
						ct,
						NotifyPhaseDetailAsync,
						NotifyPersistProgressAsync,
						ReportAcquisitionMethodAsync);
					break;
				}
				case ChannelRefreshPhase.Hydration:
					resultMessage = await channelMetadataService.RunHydrationPhaseAsync(db, channelId, ct, ReportAcquisitionMethodAsync);
					break;
				case ChannelRefreshPhase.LivestreamIdentification:
					resultMessage = await channelMetadataService.RunLivestreamIdentificationPhaseAsync(db, channelId, ct, ReportAcquisitionMethodAsync);
					break;
				case ChannelRefreshPhase.ShortsParsing:
					resultMessage = await channelMetadataService.RunShortsParsingPhaseAsync(db, channelId, ct, ReportAcquisitionMethodAsync);
					break;
				case ChannelRefreshPhase.PlaylistDiscovery:
				{
					var playlistService = scope.ServiceProvider.GetRequiredService<ChannelPlaylistDiscoveryService>();
					var (discErr, items) = await playlistService.DiscoverPlaylistsAsync(db, channelId, null, ct, ReportAcquisitionMethodAsync);
					if (discErr == null && items is null)
					{
						resultMessage = "Channel not found.";
						break;
					}

					if (!string.IsNullOrWhiteSpace(discErr))
					{
						if (IsSkippablePlaylistDiscoveryMessage(discErr))
							serializedPlaylistDiscoveryItems = "[]";
						else
							resultMessage = discErr;
						break;
					}

					serializedPlaylistDiscoveryItems = JsonSerializer.Serialize(items ?? Array.Empty<ChannelPlaylistDiscoveryItem>());
					break;
				}
				case ChannelRefreshPhase.PlaylistPopulation:
				{
					if (string.IsNullOrWhiteSpace(payload.SerializedPlaylistDiscoveryItems))
					{
						resultMessage = "Playlist population step missing discovery payload.";
						break;
					}

					List<ChannelPlaylistDiscoveryItem>? list;
					try
					{
						list = JsonSerializer.Deserialize<List<ChannelPlaylistDiscoveryItem>>(payload.SerializedPlaylistDiscoveryItems);
					}
					catch (Exception ex)
					{
						resultMessage = "Invalid playlist discovery payload: " + ex.Message;
						break;
					}

					var playlistService = scope.ServiceProvider.GetRequiredService<ChannelPlaylistDiscoveryService>();
					resultMessage = await playlistService.UpsertDiscoveredPlaylistsAsync(
						db,
						channelId,
						list ?? new List<ChannelPlaylistDiscoveryItem>(),
						null,
						ct,
						ReportAcquisitionMethodAsync);
					break;
				}
				default:
					resultMessage = "Unknown refresh phase.";
					break;
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var abortedMessage = "Refresh & Scan was aborted during shutdown.";
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body.Remove("phaseDetail");
				resource["message"] = abortedMessage;
				resource["status"] = "aborted";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			using var scope = _scopeFactory.CreateScope();
			var realtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
			await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

			await RecordScheduledTaskIfApplicableAsync(payload.Name, startedAt, completedAt, abortedMessage);
			throw;
		}
		catch (Exception ex)
		{
			resultMessage = ex.Message ?? "Unknown error";
		}

		var completedAt2 = DateTimeOffset.UtcNow;
		var success = string.IsNullOrWhiteSpace(resultMessage);
		var phaseCommandName = GetRefreshChannelPhaseCommandName(payload.Phase ?? ChannelRefreshPhase.UploadsPopulation);
		var completionMessage = success
			? $"{phaseCommandName} completed."
			: (resultMessage ?? "Unknown error.");

		var acquisitionMethods = AcquisitionMethodsJsonHelper.ToArrayOrInternalDefault(
			await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(commandId, ct));
		var updated2 = _records.UpdateCommandRecord(command, (resource, body) =>
		{
			body["sendUpdatesToClient"] = true;
			body["suppressMessages"] = false;
			body.Remove("phaseDetail");
			body["acquisitionMethods"] = acquisitionMethods;
			resource["message"] = completionMessage;
			resource["status"] = success ? "completed" : "failed";
			resource["result"] = success ? "successful" : "unsuccessful";
			resource["started"] = startedAt.ToString("O");
			resource["ended"] = completedAt2.ToString("O");
			resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt2 - startedAt);
			resource["stateChangeTime"] = completedAt2.ToString("O");
			resource["lastExecutionTime"] = completedAt2.ToString("O");
		});

		using var scope2 = _scopeFactory.CreateScope();
		var realtime2 = scope2.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		await realtime2.BroadcastAsync("command", new { action = "updated", resource = updated2 }, CancellationToken.None);

		var allChannelIds = payload.AllChannelIdsInBatch ?? payload.ChannelIds;
		var channelIndex = payload.ChannelIndexInBatch ?? 0;
		var channelId2 = payload.ChannelIds[0];

		if (success && string.Equals(payload.Phase, ChannelRefreshPhase.PlaylistPopulation, StringComparison.OrdinalIgnoreCase))
		{
			await realtime2.BroadcastAsync("channel", new { action = "sync" }, CancellationToken.None);
			await realtime2.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);
		}

		if (success && payload.StopAfterThisPhase)
		{
			if (!string.Equals(payload.Phase, ChannelRefreshPhase.PlaylistDiscovery, StringComparison.OrdinalIgnoreCase))
			{
				await realtime2.BroadcastAsync("channel", new { action = "sync" }, CancellationToken.None);
				await realtime2.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);
			}

			return;
		}

		if (success)
		{
			if (string.Equals(payload.Phase, ChannelRefreshPhase.UploadsPopulation, StringComparison.OrdinalIgnoreCase))
			{
				var next = new RefreshChannelQueueJobPayload(
					payload.Name,
					payload.Trigger,
					new[] { channelId2 },
					ChannelRefreshPhase.Hydration,
					allChannelIds,
					channelIndex,
					payload.RecordScheduledTaskForBatch,
					payload.BatchStartedAtUtc,
					SerializedPlaylistDiscoveryItems: null,
					StopAfterThisPhase: payload.StopAfterThisPhase);
				await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				return;
			}

			if (string.Equals(payload.Phase, ChannelRefreshPhase.Hydration, StringComparison.OrdinalIgnoreCase))
			{
				var next = new RefreshChannelQueueJobPayload(
					payload.Name,
					payload.Trigger,
					new[] { channelId2 },
					ChannelRefreshPhase.LivestreamIdentification,
					allChannelIds,
					channelIndex,
					payload.RecordScheduledTaskForBatch,
					payload.BatchStartedAtUtc,
					SerializedPlaylistDiscoveryItems: null,
					StopAfterThisPhase: payload.StopAfterThisPhase);
				await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				return;
			}

			if (string.Equals(payload.Phase, ChannelRefreshPhase.LivestreamIdentification, StringComparison.OrdinalIgnoreCase))
			{
				var next = new RefreshChannelQueueJobPayload(
					payload.Name,
					payload.Trigger,
					new[] { channelId2 },
					ChannelRefreshPhase.ShortsParsing,
					allChannelIds,
					channelIndex,
					payload.RecordScheduledTaskForBatch,
					payload.BatchStartedAtUtc,
					SerializedPlaylistDiscoveryItems: null,
					StopAfterThisPhase: payload.StopAfterThisPhase);
				await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				return;
			}

			if (string.Equals(payload.Phase, ChannelRefreshPhase.ShortsParsing, StringComparison.OrdinalIgnoreCase))
			{
				var next = new RefreshChannelQueueJobPayload(
					payload.Name,
					payload.Trigger,
					new[] { channelId2 },
					ChannelRefreshPhase.PlaylistDiscovery,
					allChannelIds,
					channelIndex,
					payload.RecordScheduledTaskForBatch,
					payload.BatchStartedAtUtc,
					SerializedPlaylistDiscoveryItems: null,
					StopAfterThisPhase: payload.StopAfterThisPhase);
				await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				return;
			}

			if (string.Equals(payload.Phase, ChannelRefreshPhase.PlaylistDiscovery, StringComparison.OrdinalIgnoreCase))
			{
				var next = new RefreshChannelQueueJobPayload(
					payload.Name,
					payload.Trigger,
					new[] { channelId2 },
					ChannelRefreshPhase.PlaylistPopulation,
					allChannelIds,
					channelIndex,
					payload.RecordScheduledTaskForBatch,
					payload.BatchStartedAtUtc,
					serializedPlaylistDiscoveryItems,
					StopAfterThisPhase: payload.StopAfterThisPhase);
				await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				return;
			}

			if (string.Equals(payload.Phase, ChannelRefreshPhase.PlaylistPopulation, StringComparison.OrdinalIgnoreCase))
			{
				if (channelIndex + 1 < allChannelIds.Length)
				{
					var nextChannelId = allChannelIds[channelIndex + 1];
					var next = new RefreshChannelQueueJobPayload(
						payload.Name,
						payload.Trigger,
						new[] { nextChannelId },
						ChannelRefreshPhase.UploadsPopulation,
						allChannelIds,
						channelIndex + 1,
						payload.RecordScheduledTaskForBatch,
						payload.BatchStartedAtUtc,
						SerializedPlaylistDiscoveryItems: null,
						StopAfterThisPhase: payload.StopAfterThisPhase);
					await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
				}
				else
				{
					var summary = $"Refresh & Scan completed for {allChannelIds.Length} channel(s).";
					await MaybeRecordScheduledTaskRefreshBatchAsync(payload, startedAt, completedAt2, summary);
				}
			}

			return;
		}

		if (channelIndex + 1 < allChannelIds.Length)
		{
			var nextChannelId = allChannelIds[channelIndex + 1];
			var next = new RefreshChannelQueueJobPayload(
				payload.Name,
				payload.Trigger,
				new[] { nextChannelId },
				ChannelRefreshPhase.UploadsPopulation,
				allChannelIds,
				channelIndex + 1,
				payload.RecordScheduledTaskForBatch,
				payload.BatchStartedAtUtc,
				SerializedPlaylistDiscoveryItems: null,
				StopAfterThisPhase: payload.StopAfterThisPhase);
			await EnqueueRefreshChannelPhaseAsync(realtime2, next, CancellationToken.None);
			return;
		}

		await MaybeRecordScheduledTaskRefreshBatchAsync(
			payload,
			startedAt,
			completedAt2,
			$"Refresh & Scan finished with errors. Last: {completionMessage}");
	}

	async Task ExecuteLegacyRefreshChannelAsync(
		Dictionary<string, object?> command,
		RefreshChannelQueueJobPayload payload,
		CancellationToken ct)
	{
		var startedAt = DateTimeOffset.UtcNow;
		var started = _records.UpdateCommandRecord(command, (resource, _) =>
		{
			resource["status"] = "started";
			resource["result"] = "unknown";
			resource["started"] = startedAt.ToString("O");
			resource["ended"] = null;
			resource["duration"] = null;
			resource["stateChangeTime"] = startedAt.ToString("O");
			resource["lastExecutionTime"] = startedAt.ToString("O");
		});

		using var scope0 = _scopeFactory.CreateScope();
		var realtime0 = scope0.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		await realtime0.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

		var errors = new List<string>();
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var channelMetadataService = scope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();

			foreach (var channelId in payload.ChannelIds)
			{
				ct.ThrowIfCancellationRequested();
				var err = await channelMetadataService.PopulateChannelDetailsAsync(db, channelId, ct);
				if (!string.IsNullOrWhiteSpace(err))
					errors.Add($"Channel {channelId}: {err}");
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var abortedMessage = "Refresh & Scan was aborted during shutdown.";
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body.Remove("phaseDetail");
				resource["message"] = abortedMessage;
				resource["status"] = "aborted";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			using var scope = _scopeFactory.CreateScope();
			var realtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
			await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

			await RecordScheduledTaskIfApplicableAsync(payload.Name, startedAt, completedAt, abortedMessage);
			throw;
		}
		catch (Exception ex)
		{
			errors.Add(ex.Message ?? "Unknown error");
		}

		var completedAt2 = DateTimeOffset.UtcNow;
		var errorCount = errors.Count;
		var completionMessage = errorCount == 0
			? $"Refresh & Scan completed for {payload.ChannelIds.Length} channel(s)."
			: $"{string.Join("; ", errors)} ({errorCount} error(s)).";

		var updated2 = _records.UpdateCommandRecord(command, (resource, body) =>
		{
			body["sendUpdatesToClient"] = true;
			body["suppressMessages"] = false;
			resource["message"] = completionMessage;
			resource["status"] = errorCount == 0 ? "completed" : "failed";
			resource["result"] = errorCount == 0 ? "successful" : "unsuccessful";
			resource["started"] = startedAt.ToString("O");
			resource["ended"] = completedAt2.ToString("O");
			resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt2 - startedAt);
			resource["stateChangeTime"] = completedAt2.ToString("O");
			resource["lastExecutionTime"] = completedAt2.ToString("O");
		});

		using var scope2 = _scopeFactory.CreateScope();
		var realtime2 = scope2.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		await realtime2.BroadcastAsync("command", new { action = "updated", resource = updated2 }, CancellationToken.None);
		await realtime2.BroadcastAsync("channel", new { action = "sync" }, CancellationToken.None);
		await realtime2.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);

		await RecordScheduledTaskIfApplicableAsync(payload.Name, startedAt, completedAt2, completionMessage);
	}

	async Task MaybeRecordScheduledTaskRefreshBatchAsync(
		RefreshChannelQueueJobPayload payload,
		DateTimeOffset startedAt,
		DateTimeOffset completedAt,
		string? summary)
	{
		if (!payload.RecordScheduledTaskForBatch)
			return;

		var batchStart = payload.BatchStartedAtUtc ?? startedAt;
		await RecordScheduledTaskIfApplicableAsync(payload.Name, batchStart, completedAt, summary);
	}

	static string GetRefreshChannelPhaseCommandName(string phase) =>
		phase switch
		{
			ChannelRefreshPhase.UploadsPopulation => "RefreshChannelUploadsPopulation",
			ChannelRefreshPhase.Hydration => "RefreshChannelHydration",
			ChannelRefreshPhase.LivestreamIdentification => "RefreshChannelLivestreamIdentification",
			ChannelRefreshPhase.ShortsParsing => "RefreshChannelShortsParsing",
			ChannelRefreshPhase.PlaylistDiscovery => "RefreshChannelPlaylistDiscovery",
			ChannelRefreshPhase.PlaylistPopulation => "RefreshChannelPlaylistPopulation",
			_ => "RefreshChannel"
		};

	static bool IsSkippablePlaylistDiscoveryMessage(string message) =>
		message.Contains("Only the uploads list was returned", StringComparison.OrdinalIgnoreCase) ||
		message.Contains("No playlists could be discovered for this channel", StringComparison.OrdinalIgnoreCase);

	async Task<Dictionary<string, object?>> HandleProgressAwareGetVideoDetailsAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime,
		ChannelMetadataAcquisitionService channelMetadataAcquisitionService)
	{
		var detailsChannelId = payload.TryGetProperty("channelId", out var detailsChannelIdEl) &&
			detailsChannelIdEl.ValueKind == JsonValueKind.Number &&
			detailsChannelIdEl.TryGetInt32(out var parsedDetailsChannelId)
			? parsedDetailsChannelId
			: 0;

		if (detailsChannelId <= 0)
		{
			var failedQueuedAt = DateTimeOffset.UtcNow;
			var failedBody = new Dictionary<string, object?>
			{
				["name"] = name,
				["trigger"] = trigger,
				["sendUpdatesToClient"] = true,
				["suppressMessages"] = false
			};

			var failedCommand = _records.CreateCommandRecord(
				name,
				trigger,
				failedBody,
				status: "failed",
				result: "unsuccessful",
				message: "channelId is required for Get Video Details.",
				queuedAt: failedQueuedAt,
				startedAt: failedQueuedAt,
				endedAt: failedQueuedAt);

			await _records.BroadcastCommandUpdateAsync(realtime, failedCommand);
			return _records.SnapshotCommand(failedCommand);
		}

		Dictionary<string, object?>? detailsCommand = null;
		IRealtimeEventBroadcaster progressBroadcaster = realtime;

		var progressReporter = new MetadataProgressReporter(async (snapshot, ct) =>
		{
			if (detailsCommand is null)
				return;

			var updated = _records.UpdateCommandRecord(detailsCommand, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
		});

		await progressReporter.SetStageAsync(
			"videoDetailFetching",
			"Video detail fetching",
			0,
			0,
			detail: "Preparing video detail fetch queue.");

		var detailsQueuedAt = DateTimeOffset.UtcNow;
		var detailsBody = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
			["channelId"] = detailsChannelId,
			["metadataProgress"] = _records.ToMetadataProgressResource(progressReporter.GetSnapshot())
		};

		detailsCommand = _records.CreateCommandRecord(
			name,
			trigger,
			detailsBody,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: detailsQueuedAt,
			startedAt: detailsQueuedAt,
			endedAt: detailsQueuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, detailsCommand);

		if (TryGetCommandId(detailsCommand, out var detailsCommandId))
		{
			var detailsQueuePayload = JsonSerializer.Serialize(new GetVideoDetailsQueueJobPayload(name, trigger, detailsChannelId));
			await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(detailsCommandId, name, CommandQueueJobTypes.GetVideoDetails, detailsQueuePayload, async ct =>
			{
				var startedAt = DateTimeOffset.UtcNow;
				var completedAt = startedAt;

				try
				{
					var started = _records.UpdateCommandRecord(detailsCommand, (command, _) =>
					{
						command["status"] = "started";
						command["result"] = "unknown";
						command["started"] = startedAt.ToString("O");
						command["ended"] = null;
						command["duration"] = null;
						command["stateChangeTime"] = startedAt.ToString("O");
						command["lastExecutionTime"] = startedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

					using var scope = scopeFactory.CreateScope();
					var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
					var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
					var scopedMetadataService = scope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();
					progressBroadcaster = scopedRealtime;

					async Task ReportDetailsAcquisitionMethodAsync(string methodId)
					{
						var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(detailsCommandId, methodId, ct);
						var snap = _records.UpdateCommandRecord(detailsCommand, (_, body) =>
						{
							body["acquisitionMethods"] = list.ToArray();
						});
						await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
					}

					var resultMessage = await scopedMetadataService.PopulateVideoMetadataAsync(
						scopedDb,
						detailsChannelId,
						progressReporter,
						ct,
						ReportDetailsAcquisitionMethodAsync);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
						? "Video metadata refresh completed."
						: resultMessage!;

					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var detailsAcquisitionMethods = await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(detailsCommandId, ct);
					var updated = _records.UpdateCommandRecord(detailsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						body["acquisitionMethods"] = detailsAcquisitionMethods.ToArray();
						command["message"] = completionMessage;
						command["status"] = "completed";
						command["result"] = errorCount > 0 ? "unsuccessful" : "successful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
					await scopedRealtime.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					completedAt = DateTimeOffset.UtcNow;
					var abortedMessage = "Get Video Details was aborted during shutdown.";
					await progressReporter.AddErrorAsync(abortedMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(detailsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = abortedMessage;
						command["status"] = "aborted";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
				}
				catch (Exception ex)
				{
					completedAt = DateTimeOffset.UtcNow;
					var failureMessage = "Get Video Details failed: " + (ex.Message ?? "Unknown error");
					await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(detailsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = failureMessage;
						command["status"] = "failed";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
				}
			}), CancellationToken.None);
		}

		return _records.SnapshotCommand(detailsCommand);
	}

	async Task<Dictionary<string, object?>> HandleProgressAwareGetChannelPlaylistsAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var playlistsChannelId = payload.TryGetProperty("channelId", out var playlistsChannelIdEl) &&
			playlistsChannelIdEl.ValueKind == JsonValueKind.Number &&
			playlistsChannelIdEl.TryGetInt32(out var parsedPlaylistsChannelId)
			? parsedPlaylistsChannelId
			: 0;

		if (playlistsChannelId <= 0)
		{
			var failedQueuedAt = DateTimeOffset.UtcNow;
			var failedBody = new Dictionary<string, object?>
			{
				["name"] = name,
				["trigger"] = trigger,
				["sendUpdatesToClient"] = true,
				["suppressMessages"] = false
			};

			var failedCommand = _records.CreateCommandRecord(
				name,
				trigger,
				failedBody,
				status: "failed",
				result: "unsuccessful",
				message: "channelId is required for Get Playlists.",
				queuedAt: failedQueuedAt,
				startedAt: failedQueuedAt,
				endedAt: failedQueuedAt);

			await _records.BroadcastCommandUpdateAsync(realtime, failedCommand);
			return _records.SnapshotCommand(failedCommand);
		}

		Dictionary<string, object?>? playlistsCommand = null;
		IRealtimeEventBroadcaster progressBroadcaster = realtime;

		var progressReporter = new MetadataProgressReporter(async (snapshot, ct) =>
		{
			if (playlistsCommand is null)
				return;

			var updated = _records.UpdateCommandRecord(playlistsCommand, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
		});

		await progressReporter.SetStageAsync(
			"playlistDiscovery",
			"Playlist discovery",
			0,
			0,
			detail: "Preparing playlist fetch…");

		var playlistsQueuedAt = DateTimeOffset.UtcNow;
		var playlistsBody = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
			["channelId"] = playlistsChannelId,
			["metadataProgress"] = _records.ToMetadataProgressResource(progressReporter.GetSnapshot())
		};

		playlistsCommand = _records.CreateCommandRecord(
			name,
			trigger,
			playlistsBody,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: playlistsQueuedAt,
			startedAt: playlistsQueuedAt,
			endedAt: playlistsQueuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, playlistsCommand);

		if (TryGetCommandId(playlistsCommand, out var playlistsCommandId))
		{
			var playlistsQueuePayload = JsonSerializer.Serialize(new GetChannelPlaylistsQueueJobPayload(name, trigger, playlistsChannelId));
			await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(playlistsCommandId, name, CommandQueueJobTypes.GetChannelPlaylists, playlistsQueuePayload, async ct =>
			{
				var startedAt = DateTimeOffset.UtcNow;
				var completedAt = startedAt;

				try
				{
					var started = _records.UpdateCommandRecord(playlistsCommand, (command, _) =>
					{
						command["status"] = "started";
						command["result"] = "unknown";
						command["started"] = startedAt.ToString("O");
						command["ended"] = null;
						command["duration"] = null;
						command["stateChangeTime"] = startedAt.ToString("O");
						command["lastExecutionTime"] = startedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

					using var scope = scopeFactory.CreateScope();
					var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
					var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
					var scopedPlaylistService = scope.ServiceProvider.GetRequiredService<ChannelPlaylistDiscoveryService>();
					progressBroadcaster = scopedRealtime;

					async Task ReportPlaylistsAcquisitionMethodAsync(string methodId)
					{
						var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(playlistsCommandId, methodId, ct);
						var snap = _records.UpdateCommandRecord(playlistsCommand, (_, body) =>
						{
							body["acquisitionMethods"] = list.ToArray();
						});
						await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
					}

					var resultMessage = await scopedPlaylistService.FetchAndUpsertPlaylistsAsync(
						scopedDb,
						playlistsChannelId,
						progressReporter,
						ct,
						ReportPlaylistsAcquisitionMethodAsync);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
						? "Playlists updated."
						: resultMessage!;

					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var playlistsAcquisitionMethods = await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(playlistsCommandId, ct);
					var unsuccessful = errorCount > 0 || !string.IsNullOrWhiteSpace(resultMessage);
					var updated = _records.UpdateCommandRecord(playlistsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						body["acquisitionMethods"] = playlistsAcquisitionMethods.ToArray();
						command["message"] = completionMessage;
						command["status"] = "completed";
						command["result"] = unsuccessful ? "unsuccessful" : "successful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
					await scopedRealtime.BroadcastAsync("channel", new { action = "sync" }, CancellationToken.None);
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					completedAt = DateTimeOffset.UtcNow;
					var abortedMessage = "Get Playlists was aborted during shutdown.";
					await progressReporter.AddErrorAsync(abortedMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(playlistsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = abortedMessage;
						command["status"] = "aborted";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
				}
				catch (Exception ex)
				{
					completedAt = DateTimeOffset.UtcNow;
					var failureMessage = "Get Playlists failed: " + (ex.Message ?? "Unknown error");
					await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(playlistsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = failureMessage;
						command["status"] = "failed";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
				}
			}), CancellationToken.None);
		}

		return _records.SnapshotCommand(playlistsCommand);
	}

	async Task<Dictionary<string, object?>> HandleProgressAwareRssSyncAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		int? rssOnlyChannelId = null;
		if (payload.TryGetProperty("channelId", out var rssChEl) && rssChEl.ValueKind == JsonValueKind.Number && rssChEl.TryGetInt32(out var rssCh) && rssCh > 0)
			rssOnlyChannelId = rssCh;

		Dictionary<string, object?>? rssCommand = null;
		IRealtimeEventBroadcaster progressBroadcaster = realtime;

		var progressReporter = new MetadataProgressReporter(async (snapshot, ct) =>
		{
			if (rssCommand is null)
				return;

			var updated = _records.UpdateCommandRecord(rssCommand, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
		});

		await progressReporter.SetStageAsync(
			"rssFeedSync",
			"RSS feed sync",
			0,
			0,
			detail: "Preparing RSS sync…");

		var rssQueuedAt = DateTimeOffset.UtcNow;
		var rssBody = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
			["metadataProgress"] = _records.ToMetadataProgressResource(progressReporter.GetSnapshot())
		};
		if (rssOnlyChannelId is { } rssCidBody)
			rssBody["channelId"] = rssCidBody;

		rssCommand = _records.CreateCommandRecord(
			name,
			trigger,
			rssBody,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: rssQueuedAt,
			startedAt: rssQueuedAt,
			endedAt: rssQueuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, rssCommand);

		if (TryGetCommandId(rssCommand, out var rssCommandId))
		{
			var rssQueuePayload = JsonSerializer.Serialize(new RssSyncQueueJobPayload(name, trigger, rssOnlyChannelId));
			await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(rssCommandId, name, CommandQueueJobTypes.RssSync, rssQueuePayload, async ct =>
			{
				var startedAt = DateTimeOffset.UtcNow;
				var completedAt = startedAt;

				try
				{
					var started = _records.UpdateCommandRecord(rssCommand, (command, _) =>
					{
						command["status"] = "started";
						command["result"] = "unknown";
						command["started"] = startedAt.ToString("O");
						command["ended"] = null;
						command["duration"] = null;
						command["stateChangeTime"] = startedAt.ToString("O");
						command["lastExecutionTime"] = startedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

					using var scope = scopeFactory.CreateScope();
					var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
					var scopedRss = scope.ServiceProvider.GetRequiredService<ChannelRssSyncService>();
					var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
					progressBroadcaster = scopedRealtime;

					async Task ReportAcquisitionMethodAsync(string methodId)
					{
						var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(rssCommandId, methodId, ct);
						var snap = _records.UpdateCommandRecord(rssCommand, (_, body) =>
						{
							body["acquisitionMethods"] = list.ToArray();
						});
						await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
					}

					var completionText = await scopedRss.SyncMonitoredChannelsAsync(
						scopedDb,
						rssOnlyChannelId,
						progressReporter,
						ct,
						ReportAcquisitionMethodAsync);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = string.IsNullOrWhiteSpace(completionText)
						? "RSS sync completed."
						: completionText;

					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var acquisitionMethods = await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(rssCommandId, ct);
					var updated = _records.UpdateCommandRecord(rssCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						body["acquisitionMethods"] = acquisitionMethods.ToArray();
						command["message"] = completionMessage;
						command["status"] = "completed";
						command["result"] = errorCount > 0 ? "unsuccessful" : "successful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
					await scopedRealtime.BroadcastAsync("channel", new { action = "sync" }, CancellationToken.None);
					await scopedRealtime.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);

					await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, completionMessage);
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					completedAt = DateTimeOffset.UtcNow;
					var abortedMessage = "RSS sync was aborted during shutdown.";
					await progressReporter.AddErrorAsync(abortedMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(rssCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = abortedMessage;
						command["status"] = "aborted";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

					await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, abortedMessage);
				}
				catch (Exception ex)
				{
					completedAt = DateTimeOffset.UtcNow;
					var failureMessage = "RSS sync failed: " + (ex.Message ?? "Unknown error");
					await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(rssCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
						command["message"] = failureMessage;
						command["status"] = "failed";
						command["result"] = "unsuccessful";
						command["started"] = startedAt.ToString("O");
						command["ended"] = completedAt.ToString("O");
						command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
						command["stateChangeTime"] = completedAt.ToString("O");
						command["lastExecutionTime"] = completedAt.ToString("O");
					});

					await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

					await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, failureMessage);
				}
			}), CancellationToken.None);
		}

		return _records.SnapshotCommand(rssCommand);
	}

	async Task<Dictionary<string, object?>> CreateFailedScheduledTaskCommandAsync(
		string name,
		string trigger,
		string failureMessage,
		IRealtimeEventBroadcaster realtime)
	{
		var failedAt = DateTimeOffset.UtcNow;
		var failedBody = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = true,
			["suppressMessages"] = false
		};

		var failedCommand = _records.CreateCommandRecord(
			name,
			trigger,
			failedBody,
			status: "failed",
			result: "unsuccessful",
			message: failureMessage,
			queuedAt: failedAt,
			startedAt: failedAt,
			endedAt: failedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, failedCommand);
		return _records.SnapshotCommand(failedCommand);
	}

	async Task<Dictionary<string, object?>> HandleProgressAwareRefreshMonitoredDownloadsAsync(
		string name,
		string trigger,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var queuedAt = DateTimeOffset.UtcNow;
		var body = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
		};

		var cmd = _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: queuedAt,
			startedAt: queuedAt,
			endedAt: queuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, cmd);

		if (!TryGetCommandId(cmd, out var cmdId))
			return _records.SnapshotCommand(cmd);

		var payloadJson = JsonSerializer.Serialize(new RefreshMonitoredDownloadsQueueJobPayload(name, trigger));
		await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(cmdId, name, CommandQueueJobTypes.RefreshMonitoredDownloads, payloadJson, async ct =>
		{
			var startedAt = DateTimeOffset.UtcNow;
			var completedAt = startedAt;

			try
			{
				var started = _records.UpdateCommandRecord(cmd, (command, _) =>
				{
					command["status"] = "started";
					command["result"] = "unknown";
					command["started"] = startedAt.ToString("O");
					command["ended"] = null;
					command["duration"] = null;
					command["stateChangeTime"] = startedAt.ToString("O");
					command["lastExecutionTime"] = startedAt.ToString("O");
				});

				await realtime.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

				using var scope = scopeFactory.CreateScope();
				var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
				var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
				var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
				var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

				var monitoredChannelIds = await scopedDb.Channels.AsNoTracking()
					.Where(c => c.Monitored)
					.Select(c => c.Id)
					.ToListAsync(ct);
				var metadataBusyChannelIds = monitoredChannelIds
					.Where(IsMetadataOperationInProgressForChannel)
					.ToHashSet();

				var enqueuedMissing = await DownloadQueueProcessor.EnqueueMonitoredMissingOnDiskAsync(
					scopedDb,
					metadataBusyChannelIds,
					ct,
					scopedLogger);
				await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(scopedRealtime, CancellationToken.None);

				await DownloadQueueProcessor.RunUntilEmptyAsync(
					scopeFactory,
					env.ContentRootPath,
					ct,
					scopedLogger,
					async callbackCt => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(scopedRealtime, callbackCt));

				completedAt = DateTimeOffset.UtcNow;
				var enqueueSummary = enqueuedMissing > 0
					? $"Queued {enqueuedMissing} monitored video(s) with no file on disk; download queue processed."
					: "No monitored videos missing on-disk files; download queue processed.";
				var summary = metadataBusyChannelIds.Count > 0
					? $"{enqueueSummary} Skipped {metadataBusyChannelIds.Count} channel(s) with active metadata operations."
					: enqueueSummary;

				var updated = _records.UpdateCommandRecord(cmd, (command, _) =>
				{
					command["message"] = summary;
					command["status"] = "completed";
					command["result"] = "successful";
					command["started"] = startedAt.ToString("O");
					command["ended"] = completedAt.ToString("O");
					command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
					command["stateChangeTime"] = completedAt.ToString("O");
					command["lastExecutionTime"] = completedAt.ToString("O");
				});

				await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

				await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, summary);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				completedAt = DateTimeOffset.UtcNow;
				var updated = _records.UpdateCommandRecord(cmd, (command, _) =>
				{
					command["message"] = "Download queue processing was aborted during shutdown.";
					command["status"] = "aborted";
					command["result"] = "unsuccessful";
					command["started"] = startedAt.ToString("O");
					command["ended"] = completedAt.ToString("O");
					command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
					command["stateChangeTime"] = completedAt.ToString("O");
					command["lastExecutionTime"] = completedAt.ToString("O");
				});

				await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

				await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, "Download queue processing was aborted during shutdown.");
			}
			catch (Exception ex)
			{
				completedAt = DateTimeOffset.UtcNow;
				var failureMessage = "Download queue processing failed: " + (ex.Message ?? "Unknown error");
				var updated = _records.UpdateCommandRecord(cmd, (command, _) =>
				{
					command["message"] = failureMessage;
					command["status"] = "failed";
					command["result"] = "unsuccessful";
					command["started"] = startedAt.ToString("O");
					command["ended"] = completedAt.ToString("O");
					command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
					command["stateChangeTime"] = completedAt.ToString("O");
					command["lastExecutionTime"] = completedAt.ToString("O");
				});

				await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
				logger.LogError(ex, "RefreshMonitoredDownloads failed.");

				await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, failureMessage);
			}
		}), CancellationToken.None);

		return _records.SnapshotCommand(cmd);
	}

	async Task<Dictionary<string, object?>> RunRecordedSyncCommandAsync(
		string name,
		string trigger,
		string runningMessage,
		IRealtimeEventBroadcaster realtime,
		ILogger? logger,
		Func<Func<string, Task>, Task<(bool success, string message)>> work,
		bool mirrorProgressToMetadataQueue = false)
	{
		var queuedAt = DateTimeOffset.UtcNow;
		var body = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = true,
			["suppressMessages"] = false
		};

		Dictionary<string, object?>? commandRef = null;
		MetadataProgressReporter? metaReporter = null;
		if (mirrorProgressToMetadataQueue)
		{
			metaReporter = new MetadataProgressReporter(async (snapshot, ct) =>
			{
				if (commandRef is null)
					return;

				var updated = _records.UpdateCommandRecord(commandRef, (_, b) =>
				{
					b["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				});

				await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
			});

			await metaReporter.SetStageAsync(
				"mapUnmapped",
				"Map unmapped video files",
				0,
				0,
				detail: "Queued…",
				CancellationToken.None);

			body["metadataProgress"] = _records.ToMetadataProgressResource(metaReporter.GetSnapshot());
		}

		var command = _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: queuedAt,
			startedAt: queuedAt,
			endedAt: queuedAt);

		if (mirrorProgressToMetadataQueue)
			commandRef = command;

		await _records.BroadcastCommandUpdateAsync(realtime, command);

		var startedAt = DateTimeOffset.UtcNow;
		var started = _records.UpdateCommandRecord(command, (c, _) =>
		{
			c["status"] = "started";
			c["result"] = "unknown";
			c["started"] = startedAt.ToString("O");
			c["ended"] = null;
			c["duration"] = null;
			c["stateChangeTime"] = startedAt.ToString("O");
			c["lastExecutionTime"] = startedAt.ToString("O");
			c["message"] = runningMessage;
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

		async Task ReportProgressAsync(string detail)
		{
			logger?.LogInformation("Command {CommandName}: {CommandMessage}", name, detail);
			if (metaReporter is not null)
			{
				await metaReporter.SetStageAsync(
					"mapUnmapped",
					"Map unmapped video files",
					0,
					0,
					detail: detail,
					CancellationToken.None);
			}

			var updated = _records.UpdateCommandRecord(command, (c, _) => { c["message"] = detail; });
			await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
		}

		var (success, message) = await work(ReportProgressAsync);
		var completedAt = DateTimeOffset.UtcNow;
		var updated = _records.UpdateCommandRecord(command, (c, b) =>
		{
			b["sendUpdatesToClient"] = true;
			b["suppressMessages"] = false;
			c["message"] = message;
			c["status"] = success ? "completed" : "failed";
			c["result"] = success ? "successful" : "unsuccessful";
			c["started"] = startedAt.ToString("O");
			c["ended"] = completedAt.ToString("O");
			c["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
			c["stateChangeTime"] = completedAt.ToString("O");
			c["lastExecutionTime"] = completedAt.ToString("O");
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

		await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, message);

		return _records.SnapshotCommand(command);
	}

	async Task<Dictionary<string, object?>> HandleCleanUpRecycleBinCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		using var scope = scopeFactory.CreateScope();
		var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Cleaning up recycle bin…",
			realtime,
			logger,
			async _ =>
			{
				var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
				var days = media?.RecycleBinCleanupDays ?? 7;
				var (deleted, errors, msg) = RecycleBinCleanupHelper.CleanupOldFiles(
					media?.RecycleBin,
					days,
					env.ContentRootPath,
					logger);

				var success = errors == 0;
				return (success, msg);
			});
	}

	async Task<Dictionary<string, object?>> HandleHousekeepingCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		using var scope = scopeFactory.CreateScope();
		var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Running housekeeping…",
			realtime,
			logger,
			async report =>
			{
				var (_, _, msg) = await HousekeepingRunner.RunAsync(db, logger, env.ContentRootPath, report, CancellationToken.None);
				return (true, msg);
			});
	}

	async Task<Dictionary<string, object?>> HandleSyncCustomNfosCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (media?.UseCustomNfos == false)
		{
			return await RunRecordedSyncCommandAsync(
				name,
				trigger,
				"Syncing library NFOs…",
				realtime,
				logger,
				_ => Task.FromResult((false, "Custom NFOs are disabled in Media Management settings.")));
		}

		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Syncing library NFOs…",
			realtime,
			logger,
			async _ =>
			{
				var (_, _, msg) = await NfoLibrarySyncRunner.RunAsync(db, logger, CancellationToken.None);
				return (true, msg);
			});
	}

	async Task<Dictionary<string, object?>> HandleRepairLibraryNfosAndArtworkCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Downloading new thumbnails…",
			realtime,
			logger,
			async report =>
			{
				using var scope = scopeFactory.CreateScope();
				var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
				var (_, _, _, msg) = await LibraryNfoAndArtworkRepairRunner.RunAsync(db, http, logger, CancellationToken.None, reportProgress: report);
				return (true, msg);
			});
	}

	async Task<Dictionary<string, object?>> HandleRenameFilesCommandAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var channelId = payload.TryGetProperty("channelId", out var cidEl) && cidEl.ValueKind == JsonValueKind.Number && cidEl.TryGetInt32(out var cid)
			? cid
			: 0;
		if (channelId <= 0)
			return await CreateFailedScheduledTaskCommandAsync(name, trigger, "channelId is required for Rename Files.", realtime);

		var ids = new List<int>();
		if (payload.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
		{
			foreach (var el in filesEl.EnumerateArray())
			{
				if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id) && id > 0)
					ids.Add(id);
			}
		}

		ids = ids.Distinct().ToList();
		if (ids.Count == 0)
			return await CreateFailedScheduledTaskCommandAsync(name, trigger, "No files selected for Rename Files.", realtime);

		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Renaming files…",
			realtime,
			logger,
			async report =>
			{
				var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
				if (!naming.RenameVideos)
					return (false, "Renaming is disabled (Settings → Naming → Rename Videos).");

				var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
				var useCustomNfos = media?.UseCustomNfos != false;

				var roots = await db.RootFolders.AsNoTracking().ToListAsync();
				var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
				if (channel is null)
					return (false, "Channel not found.");

				var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(
					channel,
					new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow },
					naming,
					roots);
				if (string.IsNullOrWhiteSpace(showRoot))
					return (false, "Channel folder could not be resolved (missing root folder or naming).");

				string ResolveVideoPattern()
				{
					var ctRaw = (channel.ChannelType ?? "").Trim().ToLowerInvariant();
					return ctRaw switch
					{
						"daily" => naming.DailyVideoFormat,
						"episodic" => naming.EpisodicVideoFormat,
						"streaming" => naming.StreamingVideoFormat,
						_ => naming.StandardVideoFormat
					};
				}

				var videoPattern = ResolveVideoPattern();
				var patternForTokens = videoPattern ?? string.Empty;
				var needsPlaylistNumber = patternForTokens.Contains("{Playlist Number", StringComparison.OrdinalIgnoreCase);
				var needsPlaylistIndex = patternForTokens.Contains("{Playlist Index", StringComparison.OrdinalIgnoreCase);

				var selected = await db.VideoFiles
					.Where(vf => vf.ChannelId == channelId && ids.Contains(vf.Id) && vf.Path != null && vf.Path != "")
					.ToListAsync();
				if (selected.Count == 0)
					return (false, "No matching tracked video files found for the selected ids.");

				var videoIds = selected.Select(vf => vf.VideoId).Distinct().ToList();
				var videos = await db.Videos.AsNoTracking()
					.Where(v => videoIds.Contains(v.Id))
					.ToDictionaryAsync(v => v.Id);

				var playlists = await db.Playlists.AsNoTracking()
					.Where(p => p.ChannelId == channelId)
					.ToListAsync();
				var playlistById = playlists.ToDictionary(p => p.Id);

				var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(db, channelId, videoIds, CancellationToken.None);

				var candidates = new List<(VideoFileEntity Vf, VideoEntity Video, int? PrimaryPlaylistId, PlaylistEntity? Playlist, int? PlaylistNumber, int? SeasonNumber, string OutputDir, string Ext)>();
				for (var i = 0; i < selected.Count; i++)
				{
					var vf = selected[i];
					if (string.IsNullOrWhiteSpace(vf.Path) || !File.Exists(vf.Path))
						continue;
					if (!videos.TryGetValue(vf.VideoId, out var video))
						continue;

					var primaryPlaylistId = primaryPlaylistByVideoId.GetValueOrDefault(video.Id);
					PlaylistEntity? playlist = null;
					if (primaryPlaylistId.HasValue && playlistById.TryGetValue(primaryPlaylistId.Value, out var pl))
						playlist = pl;

					int? playlistNumberToken = null;
					int? seasonNumber = null;
					if (needsPlaylistNumber || (channel.PlaylistFolder == true && useCustomNfos))
					{
						var (sn, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channelId, video, primaryPlaylistId, CancellationToken.None);
						if (needsPlaylistNumber)
							playlistNumberToken = sn;
						if (channel.PlaylistFolder == true && useCustomNfos)
							seasonNumber = sn;
					}

					var outputDir = DownloadQueueProcessor.GetOutputDirectory(
						channel,
						video,
						playlist,
						naming,
						roots,
						useCustomNfos,
						seasonNumber);
					if (string.IsNullOrWhiteSpace(outputDir))
						continue;

					var ext = Path.GetExtension(vf.Path);
					candidates.Add((vf, video, primaryPlaylistId, playlist, playlistNumberToken, seasonNumber, outputDir, ext));
				}

				Dictionary<int, int>? customIndexByVideoId = null;
				if (needsPlaylistIndex)
				{
					var customGroups = candidates
						.Where(x => x.PlaylistNumber is >= (NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1))
						.GroupBy(x => x.PlaylistNumber!.Value)
						.ToList();

					if (customGroups.Count > 0)
					{
						customIndexByVideoId = new Dictionary<int, int>();
						foreach (var g in customGroups)
						{
							var ordered = g
								.OrderBy(x => x.Video.UploadDateUtc)
								.ThenBy(x => x.Video.Id)
								.ToList();
							for (var idx = 0; idx < ordered.Count; idx++)
							{
								customIndexByVideoId[ordered[idx].Video.Id] = idx + 1;
							}
						}
					}
				}

				var moved = 0;
				var skipped = 0;
				var errors = 0;

				for (var i = 0; i < candidates.Count; i++)
				{
					var c = candidates[i];

					var vf = c.Vf;
					var video = c.Video;
					var primaryPlaylistId = c.PrimaryPlaylistId;
					var playlist = c.Playlist;

					int? playlistIndexToken = null;
					if (needsPlaylistIndex)
					{
						if (customIndexByVideoId is not null && customIndexByVideoId.TryGetValue(video.Id, out var customIndex))
						{
							playlistIndexToken = customIndex;
						}
						else
						{
							var n = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, primaryPlaylistId, video.Id, CancellationToken.None);
							playlistIndexToken = n;
						}
					}
					var ctx = new VideoFileNaming.NamingContext(
						Channel: channel,
						Playlist: playlist,
						Video: video,
						PlaylistIndex: playlistIndexToken,
						QualityFull: null,
						Resolution: null,
						Extension: c.Ext,
						PlaylistNumber: c.PlaylistNumber);
					var newFileName = VideoFileNaming.BuildFileName(videoPattern ?? string.Empty, ctx, naming);
					if (string.IsNullOrWhiteSpace(newFileName))
					{
						errors++;
						continue;
					}

					var destinationPath = Path.Combine(c.OutputDir, newFileName + c.Ext);
					var sourcePath = vf.Path!;

					var same = OperatingSystem.IsWindows()
						? string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)
						: string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.Ordinal);
					if (same)
					{
						skipped++;
						continue;
					}

					var chTitle = string.IsNullOrWhiteSpace(channel.Title) ? $"Channel {channel.Id}" : channel.Title.Trim();
					var plTitle = playlist is null ? "Uploads" : (string.IsNullOrWhiteSpace(playlist.Title) ? $"Playlist {playlist.Id}" : playlist.Title.Trim());
					var vidTitle = string.IsNullOrWhiteSpace(video.Title) ? (video.YoutubeVideoId ?? $"Video {video.Id}") : video.Title.Trim();
					var vidId = string.IsNullOrWhiteSpace(video.YoutubeVideoId) ? "" : $" [{video.YoutubeVideoId.Trim()}]";
					await report($"Renaming: {chTitle} / {plTitle} / {vidTitle}{vidId} ({i + 1}/{candidates.Count})");

					try
					{
						var destDir = Path.GetDirectoryName(destinationPath);
						if (!string.IsNullOrEmpty(destDir))
							Directory.CreateDirectory(destDir);

						if (File.Exists(destinationPath))
						{
							errors++;
							continue;
						}

						File.Move(sourcePath, destinationPath);

						// Move common sidecars if present (episode nfo/thumb).
						var srcBase = Path.Combine(Path.GetDirectoryName(sourcePath) ?? "", Path.GetFileNameWithoutExtension(sourcePath));
						var destBase = Path.Combine(Path.GetDirectoryName(destinationPath) ?? "", Path.GetFileNameWithoutExtension(destinationPath));
						TryMoveSidecar(srcBase + ".nfo", destBase + ".nfo");
						TryMoveSidecar(srcBase + "-thumb.jpg", destBase + "-thumb.jpg");

						vf.Path = destinationPath;
						vf.RelativePath = Path.GetRelativePath(showRoot, destinationPath).Replace('\\', '/');
						vf.DateAdded = DateTimeOffset.UtcNow;

						db.DownloadHistory.Add(new DownloadHistoryEntity
						{
							ChannelId = channelId,
							VideoId = video.Id,
							PlaylistId = primaryPlaylistId,
							EventType = 6,
							SourceTitle = video.Title ?? video.YoutubeVideoId ?? "",
							OutputPath = vf.RelativePath,
							Message = $"Renamed to {vf.RelativePath}",
							Date = DateTime.UtcNow
						});

						moved++;
					}
					catch (Exception ex)
					{
						errors++;
						logger.LogWarning(ex, "RenameFiles failed videoFileId={VideoFileId}", vf.Id);
					}
				}

				await db.SaveChangesAsync();
				await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, CancellationToken.None);

				var msg = $"Renamed {moved} file(s), skipped {skipped}, errors {errors}.";
				return (errors == 0, msg);

				static void TryMoveSidecar(string src, string dest)
				{
					try
					{
						if (!File.Exists(src))
							return;
						var dir = Path.GetDirectoryName(dest);
						if (!string.IsNullOrEmpty(dir))
							Directory.CreateDirectory(dir);
						if (File.Exists(dest))
							return;
						File.Move(src, dest);
					}
					catch
					{
						// best-effort
					}
				}
			});
	}

	async Task<Dictionary<string, object?>> HandleRenameChannelCommandAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var channelIds = new List<int>();
		if (payload.TryGetProperty("channelId", out var channelIdEl) &&
			channelIdEl.ValueKind == JsonValueKind.Number &&
			channelIdEl.TryGetInt32(out var singleChannelId))
		{
			channelIds.Add(singleChannelId);
		}

		if (payload.TryGetProperty("channelIds", out var channelIdsEl) && channelIdsEl.ValueKind == JsonValueKind.Array)
		{
			foreach (var channelIdValue in channelIdsEl.EnumerateArray())
			{
				if (channelIdValue.ValueKind == JsonValueKind.Number && channelIdValue.TryGetInt32(out var channelId))
					channelIds.Add(channelId);
			}
		}

		channelIds = channelIds.Distinct().Where(x => x > 0).ToList();
		if (channelIds.Count == 0)
			return await CreateFailedScheduledTaskCommandAsync(name, trigger, "channelId/channelIds is required for Rename Channel.", realtime);

		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Organizing files…",
			realtime,
			logger,
			async report =>
			{
				var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
				if (!naming.RenameVideos)
					return (false, "Renaming is disabled (Settings → Naming → Rename Videos).");

				var renamed = 0;
				var skipped = 0;
				var errors = 0;

				for (var i = 0; i < channelIds.Count; i++)
				{
					var channelId = channelIds[i];
					await report($"Organizing channel {i + 1}/{channelIds.Count}…");

					var ids = await db.VideoFiles
						.AsNoTracking()
						.Where(vf => vf.ChannelId == channelId && vf.Path != null && vf.Path != "")
						.Select(vf => vf.Id)
						.ToListAsync();

					if (ids.Count == 0)
						continue;

					var result = await RenameFilesForChannelAsync(channelId, ids, report);
					if (!result.Success)
					{
						errors++;
						continue;
					}

					renamed += result.Moved;
					skipped += result.Skipped;
					errors += result.Errors;
				}

				var msg = $"Renamed {renamed} file(s), skipped {skipped}, errors {errors}.";
				return (errors == 0, msg);

				async Task<(bool Success, int Moved, int Skipped, int Errors)> RenameFilesForChannelAsync(
					int channelId,
					List<int> ids,
					Func<string, Task> reportInner)
				{
					ids = ids.Distinct().ToList();
					if (ids.Count == 0)
						return (true, 0, 0, 0);

					// Inline the existing RenameFiles logic by invoking the same code path.
					// We can't call HandleRenameFilesCommandAsync directly without rebuilding JsonElement payload.
					// Keep this behavior aligned with RenameFiles by sharing the implementation below.
					var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
					var useCustomNfos = media?.UseCustomNfos != false;

					var roots = await db.RootFolders.AsNoTracking().ToListAsync();
					var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
					if (channel is null)
						return (false, 0, 0, 0);

					var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(
						channel,
						new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow },
						naming,
						roots);
					if (string.IsNullOrWhiteSpace(showRoot))
						return (false, 0, 0, 0);

					string ResolveVideoPattern()
					{
						var ctRaw = (channel.ChannelType ?? "").Trim().ToLowerInvariant();
						return ctRaw switch
						{
							"daily" => naming.DailyVideoFormat,
							"episodic" => naming.EpisodicVideoFormat,
							"streaming" => naming.StreamingVideoFormat,
							_ => naming.StandardVideoFormat
						};
					}

					var videoPattern = ResolveVideoPattern();
					var patternForTokens = videoPattern ?? string.Empty;
					var needsPlaylistNumber = patternForTokens.Contains("{Playlist Number", StringComparison.OrdinalIgnoreCase);
					var needsPlaylistIndex = patternForTokens.Contains("{Playlist Index", StringComparison.OrdinalIgnoreCase);

					var selected = await db.VideoFiles
						.Where(vf => vf.ChannelId == channelId && ids.Contains(vf.Id) && vf.Path != null && vf.Path != "")
						.ToListAsync();
					if (selected.Count == 0)
						return (true, 0, 0, 0);

					var videoIds = selected.Select(vf => vf.VideoId).Distinct().ToList();
					var videos = await db.Videos.AsNoTracking()
						.Where(v => videoIds.Contains(v.Id))
						.ToDictionaryAsync(v => v.Id);

					var playlists = await db.Playlists.AsNoTracking()
						.Where(p => p.ChannelId == channelId)
						.ToListAsync();
					var playlistById = playlists.ToDictionary(p => p.Id);

					var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(db, channelId, videoIds, CancellationToken.None);

					var candidates = new List<(VideoFileEntity Vf, VideoEntity Video, int? PrimaryPlaylistId, PlaylistEntity? Playlist, int? PlaylistNumber, int? SeasonNumber, string OutputDir, string Ext)>();
					for (var i = 0; i < selected.Count; i++)
					{
						var vf = selected[i];
						if (string.IsNullOrWhiteSpace(vf.Path) || !File.Exists(vf.Path))
							continue;
						if (!videos.TryGetValue(vf.VideoId, out var video))
							continue;

						var primaryPlaylistId = primaryPlaylistByVideoId.GetValueOrDefault(video.Id);
						PlaylistEntity? playlist = null;
						if (primaryPlaylistId.HasValue && playlistById.TryGetValue(primaryPlaylistId.Value, out var pl))
							playlist = pl;

						int? playlistNumberToken = null;
						int? seasonNumber = null;
						if (needsPlaylistNumber || (channel.PlaylistFolder == true && useCustomNfos))
						{
							var (sn, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channelId, video, primaryPlaylistId, CancellationToken.None);
							if (needsPlaylistNumber)
								playlistNumberToken = sn;
							if (channel.PlaylistFolder == true && useCustomNfos)
								seasonNumber = sn;
						}

						var outputDir = DownloadQueueProcessor.GetOutputDirectory(
							channel,
							video,
							playlist,
							naming,
							roots,
							useCustomNfos,
							seasonNumber);
						if (string.IsNullOrWhiteSpace(outputDir))
							continue;

						var ext = Path.GetExtension(vf.Path);
						candidates.Add((vf, video, primaryPlaylistId, playlist, playlistNumberToken, seasonNumber, outputDir, ext));
					}

					Dictionary<int, int>? customIndexByVideoId = null;
					if (needsPlaylistIndex)
					{
						var customGroups = candidates
							.Where(x => x.PlaylistNumber is >= (NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1))
							.GroupBy(x => x.PlaylistNumber!.Value)
							.ToList();

						if (customGroups.Count > 0)
						{
							customIndexByVideoId = new Dictionary<int, int>();
							foreach (var g in customGroups)
							{
								var ordered = g
									.OrderBy(x => x.Video.UploadDateUtc)
									.ThenBy(x => x.Video.Id)
									.ToList();
								for (var idx = 0; idx < ordered.Count; idx++)
								{
									customIndexByVideoId[ordered[idx].Video.Id] = idx + 1;
								}
							}
						}
					}

					var moved = 0;
					var skipped = 0;
					var errors = 0;

					for (var i = 0; i < candidates.Count; i++)
					{
						var c = candidates[i];

						var vf = c.Vf;
						var video = c.Video;
						var primaryPlaylistId = c.PrimaryPlaylistId;
						var playlist = c.Playlist;

						int? playlistIndexToken = null;
						if (needsPlaylistIndex)
						{
							if (customIndexByVideoId is not null && customIndexByVideoId.TryGetValue(video.Id, out var customIndex))
							{
								playlistIndexToken = customIndex;
							}
							else
							{
								var n = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, primaryPlaylistId, video.Id, CancellationToken.None);
								playlistIndexToken = n;
							}
						}
						var ctx = new VideoFileNaming.NamingContext(
							Channel: channel,
							Playlist: playlist,
							Video: video,
							PlaylistIndex: playlistIndexToken,
							QualityFull: null,
							Resolution: null,
							Extension: c.Ext,
							PlaylistNumber: c.PlaylistNumber);
						var newFileName = VideoFileNaming.BuildFileName(videoPattern ?? string.Empty, ctx, naming);
						if (string.IsNullOrWhiteSpace(newFileName))
						{
							errors++;
							continue;
						}

						var destinationPath = Path.Combine(c.OutputDir, newFileName + c.Ext);
						var sourcePath = vf.Path!;

						var same = OperatingSystem.IsWindows()
							? string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)
							: string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.Ordinal);
						if (same)
						{
							skipped++;
							continue;
						}

						var chTitle = string.IsNullOrWhiteSpace(channel.Title) ? $"Channel {channel.Id}" : channel.Title.Trim();
						var plTitle = playlist is null ? "Uploads" : (string.IsNullOrWhiteSpace(playlist.Title) ? $"Playlist {playlist.Id}" : playlist.Title.Trim());
						var vidTitle = string.IsNullOrWhiteSpace(video.Title) ? (video.YoutubeVideoId ?? $"Video {video.Id}") : video.Title.Trim();
						var vidId = string.IsNullOrWhiteSpace(video.YoutubeVideoId) ? "" : $" [{video.YoutubeVideoId.Trim()}]";
						await reportInner($"Renaming: {chTitle} / {plTitle} / {vidTitle}{vidId} ({i + 1}/{candidates.Count})");

						try
						{
							var destDir = Path.GetDirectoryName(destinationPath);
							if (!string.IsNullOrEmpty(destDir))
								Directory.CreateDirectory(destDir);

							if (File.Exists(destinationPath))
							{
								errors++;
								continue;
							}

							File.Move(sourcePath, destinationPath);

							var srcBase = Path.Combine(Path.GetDirectoryName(sourcePath) ?? "", Path.GetFileNameWithoutExtension(sourcePath));
							var destBase = Path.Combine(Path.GetDirectoryName(destinationPath) ?? "", Path.GetFileNameWithoutExtension(destinationPath));
							TryMoveSidecar(srcBase + ".nfo", destBase + ".nfo");
							TryMoveSidecar(srcBase + "-thumb.jpg", destBase + "-thumb.jpg");

							vf.Path = destinationPath;
							vf.RelativePath = Path.GetRelativePath(showRoot, destinationPath).Replace('\\', '/');
							vf.DateAdded = DateTimeOffset.UtcNow;

							db.DownloadHistory.Add(new DownloadHistoryEntity
							{
								ChannelId = channelId,
								VideoId = video.Id,
								PlaylistId = primaryPlaylistId,
								EventType = 6,
								SourceTitle = video.Title ?? video.YoutubeVideoId ?? "",
								OutputPath = vf.RelativePath,
								Message = $"Renamed to {vf.RelativePath}",
								Date = DateTime.UtcNow
							});

							moved++;
						}
						catch (Exception ex)
						{
							errors++;
							logger.LogWarning(ex, "RenameChannel failed videoFileId={VideoFileId}", vf.Id);
						}
					}

					await db.SaveChangesAsync();
					await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, CancellationToken.None);

					return (errors == 0, moved, skipped, errors);

					static void TryMoveSidecar(string src, string dest)
					{
						try
						{
							if (!File.Exists(src))
								return;
							var dir = Path.GetDirectoryName(dest);
							if (!string.IsNullOrEmpty(dir))
								Directory.CreateDirectory(dir);
							if (File.Exists(dest))
								return;
							File.Move(src, dest);
						}
						catch
						{
							// best-effort
						}
					}
				}
			});
	}

	async Task<Dictionary<string, object?>> HandleMapUnmappedVideoFilesCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		var result = await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Mapping unmapped video files…",
			realtime,
			logger,
			async report =>
			{
				await report("Scanning channel folders for media files…");
				var (_, mapMsg) = await UnmappedVideoFileMappingRunner.RunAsync(db, logger, CancellationToken.None, report);
				var (_, probeMsg) = await VideoFileFfProbeEnricher.RunAsync(
					db,
					logger,
					CancellationToken.None,
					reportProgress: null,
					channelId: null,
					reportFileProgress: async (completed, total, fileName) =>
					{
						var line = completed == 0
							? $"ffprobe: {total} file(s) queued (this may take a while)…"
							: $"ffprobe: {completed}/{total}: {fileName}";
						await report(line);
					});
				return (true, $"{mapMsg.TrimEnd()} {probeMsg}".Trim());
			},
			mirrorProgressToMetadataQueue: true);

		await realtime.BroadcastAsync("video", new { action = "sync" }, CancellationToken.None);

		return result;
	}

	async Task<Dictionary<string, object?>> HandleCheckHealthCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IRealtimeEventBroadcaster realtime,
		YouTubeDataApiMetadataService youTubeDataApiMetadataService)
	{
		var queuedAt = DateTimeOffset.UtcNow;
		var body = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = true,
			["suppressMessages"] = false
		};

		var command = _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: queuedAt,
			startedAt: queuedAt,
			endedAt: queuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, command);

		var startedAt = DateTimeOffset.UtcNow;
		var started = _records.UpdateCommandRecord(command, (c, _) =>
		{
			c["status"] = "started";
			c["result"] = "unknown";
			c["started"] = startedAt.ToString("O");
			c["ended"] = null;
			c["duration"] = null;
			c["stateChangeTime"] = startedAt.ToString("O");
			c["lastExecutionTime"] = startedAt.ToString("O");
			c["message"] = "Running health checks…";
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

		var checks = await TubeArrHealthCheckRunner.CollectAsync(db, youTubeDataApiMetadataService, CancellationToken.None);
		var errorCount = checks.Count(c =>
			c.TryGetValue("status", out var s) &&
			string.Equals(s?.ToString(), "error", StringComparison.OrdinalIgnoreCase));
		var success = errorCount == 0;
		var summary = TubeArrHealthCheckRunner.Summarize(checks);
		var completedAt = DateTimeOffset.UtcNow;

		var updated = _records.UpdateCommandRecord(command, (c, b) =>
		{
			b["sendUpdatesToClient"] = true;
			b["suppressMessages"] = false;
			b["healthChecks"] = checks;
			c["message"] = summary;
			c["status"] = success ? "completed" : "failed";
			c["result"] = success ? "successful" : "unsuccessful";
			c["started"] = startedAt.ToString("O");
			c["ended"] = completedAt.ToString("O");
			c["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
			c["stateChangeTime"] = completedAt.ToString("O");
			c["lastExecutionTime"] = completedAt.ToString("O");
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

		await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, summary);

		return _records.SnapshotCommand(command);
	}

	async Task<Dictionary<string, object?>> HandleApplicationUpdateCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		IRealtimeEventBroadcaster realtime)
	{
		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Checking for application updates…",
			realtime,
			logger: null,
			async _ =>
			{
				using var scope = scopeFactory.CreateScope();
				var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
				var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
				return await ApplicationUpdateChecker.CheckAsync(db, configuration, httpFactory, CancellationToken.None);
			});
	}

	async Task<Dictionary<string, object?>> HandleMessagingCleanupCommandAsync(
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime)
	{
		return await RunRecordedSyncCommandAsync(
			name,
			trigger,
			"Pruning old activity history…",
			realtime,
			logger,
			async _ =>
			{
				using var scope = scopeFactory.CreateScope();
				var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
				var days = 90;
				if (int.TryParse(configuration["TubeArr:DownloadHistoryRetentionDays"], out var parsed) && parsed > 0)
					days = parsed;

				var (_, msg) = await DownloadHistoryCleanupHelper.PruneOlderThanAsync(db, days, logger, CancellationToken.None);
				return (true, msg);
			});
	}

	async Task<Dictionary<string, object?>> HandleBackupCommandAsync(
		string name,
		string trigger,
		IServiceScopeFactory scopeFactory,
		IRealtimeEventBroadcaster realtime)
	{
		var queuedAt = DateTimeOffset.UtcNow;
		var body = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = true,
			["suppressMessages"] = false
		};

		var command = _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: queuedAt,
			startedAt: queuedAt,
			endedAt: queuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, command);

		var startedAt = DateTimeOffset.UtcNow;
		var started = _records.UpdateCommandRecord(command, (command, _) =>
		{
			command["status"] = "started";
			command["result"] = "unknown";
			command["started"] = startedAt.ToString("O");
			command["ended"] = null;
			command["duration"] = null;
			command["stateChangeTime"] = startedAt.ToString("O");
			command["lastExecutionTime"] = startedAt.ToString("O");
			command["message"] = "Backing up database.";
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = started }, CancellationToken.None);

		var backupType = string.Equals(trigger, "scheduled", StringComparison.OrdinalIgnoreCase) ? "scheduled" : "manual";
		var (success, message) = await _backupRestore.CreateBackupAsync(scopeFactory, trigger, backupType);

		var completedAt = DateTimeOffset.UtcNow;
		var updated = _records.UpdateCommandRecord(command, (command, body) =>
		{
			body["sendUpdatesToClient"] = true;
			body["suppressMessages"] = false;
			command["message"] = message;
			command["status"] = success ? "completed" : "failed";
			command["result"] = success ? "successful" : "unsuccessful";
			command["started"] = startedAt.ToString("O");
			command["ended"] = completedAt.ToString("O");
			command["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
			command["stateChangeTime"] = completedAt.ToString("O");
			command["lastExecutionTime"] = completedAt.ToString("O");
		});

		await realtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

		await RecordScheduledTaskIfApplicableAsync(name, startedAt, completedAt, message);

		return _records.SnapshotCommand(command);
	}

	async Task<Dictionary<string, object?>> HandleLegacyCommandAsync(
		JsonElement payload,
		string name,
		string trigger,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime,
		ChannelMetadataAcquisitionService channelMetadataAcquisitionService)
	{
		// RefreshChannel: refresh channel and video metadata using the direct-first acquisition flow
		var refreshChannelMessage = "";
		var refreshQueued = DateTimeOffset.UtcNow;
		var refreshStarted = refreshQueued;
		var refreshEnded = refreshQueued;

		if (string.Equals(name, "RefreshChannel", StringComparison.OrdinalIgnoreCase))
		{
			var channelIds = new List<int>();
			if (payload.TryGetProperty("channelId", out var channelIdEl))
			{
				if (channelIdEl.ValueKind == JsonValueKind.Number && channelIdEl.TryGetInt32(out var id))
					channelIds.Add(id);
			}

			if (payload.TryGetProperty("channelIds", out var channelIdsEl) && channelIdsEl.ValueKind == JsonValueKind.Array)
			{
				foreach (var channelIdValue in channelIdsEl.EnumerateArray())
				{
					if (channelIdValue.ValueKind == JsonValueKind.Number && channelIdValue.TryGetInt32(out var channelId))
						channelIds.Add(channelId);
				}
			}

			channelIds = channelIds
				.Where(x => x > 0)
				.Distinct()
				.ToList();

			if (channelIds.Count > 0)
			{
				refreshStarted = DateTimeOffset.UtcNow;
				var refreshMessages = new List<string>();
				foreach (var channelId in channelIds)
				{
					try
					{
						var msg = await channelMetadataAcquisitionService.PopulateChannelDetailsAsync(db, channelId, CancellationToken.None);
						if (!string.IsNullOrWhiteSpace(msg))
							refreshMessages.Add(msg);
					}
					catch (Exception ex)
					{
						refreshMessages.Add($"Channel {channelId}: Refresh & Scan failed: " + (ex.Message ?? "Unknown error"));
					}
				}

				if (refreshMessages.Count > 0)
					refreshChannelMessage = string.Join(" ", refreshMessages);

				refreshEnded = DateTimeOffset.UtcNow;
			}
			else
			{
				refreshChannelMessage = "channelId is required for Refresh & Scan.";
			}
		}

		// DownloadMonitored: enqueue all monitored videos for the channel, then process queue in background
		if (string.Equals(name, "DownloadMonitored", StringComparison.OrdinalIgnoreCase))
		{
			var downloadChannelId = 0;
			int? downloadPlaylistNumber = null;

			if (payload.TryGetProperty("channelId", out var dcidEl) && dcidEl.ValueKind == JsonValueKind.Number && dcidEl.TryGetInt32(out var dcid))
				downloadChannelId = dcid;

			if (payload.TryGetProperty("playlistNumber", out var playlistNumberEl) && playlistNumberEl.ValueKind == JsonValueKind.Number && playlistNumberEl.TryGetInt32(out var pn))
				downloadPlaylistNumber = pn;

			if (downloadChannelId > 0)
			{
				if (IsMetadataOperationInProgressForChannel(downloadChannelId))
				{
					refreshChannelMessage = "Cannot start downloads while metadata operations are running for this channel.";
				}
				else
				{
				try
				{
					var explicitVideoIds = new List<int>();
					if (payload.TryGetProperty("videoIds", out var vidArrEl) && vidArrEl.ValueKind == JsonValueKind.Array)
					{
						foreach (var el in vidArrEl.EnumerateArray())
						{
							if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var vid) && vid > 0)
								explicitVideoIds.Add(vid);
						}
					}

					int added;
					if (explicitVideoIds.Count > 0)
					{
						added = await DownloadQueueProcessor.EnqueueVideosAsync(db, downloadChannelId, explicitVideoIds, default, logger);
						refreshChannelMessage = added > 0
							? $"Queued {added} video(s) for download."
							: "No new videos to queue (none matched this channel or already queued).";
					}
					else
					{
						added = await DownloadQueueProcessor.EnqueueMonitoredVideosAsync(db, downloadChannelId, downloadPlaylistNumber, default, logger);
						var scopeLabel = downloadPlaylistNumber.HasValue
							? $"playlist {downloadPlaylistNumber.Value}"
							: "channel";

						refreshChannelMessage = added > 0
							? $"Queued {added} monitored video(s) for download ({scopeLabel})."
							: $"No new monitored videos to queue for {scopeLabel} (none or already queued).";
					}

					refreshStarted = DateTimeOffset.UtcNow;
					refreshEnded = DateTimeOffset.UtcNow;

					await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);

					var downloadPumpPayload = JsonSerializer.Serialize(new DownloadMonitoredQueuePumpPayload("DownloadMonitoredQueuePump"));
					await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(null, "DownloadMonitoredQueuePump", CommandQueueJobTypes.DownloadMonitoredQueuePump, downloadPumpPayload, async ct =>
					{
						try
						{
							using var scope = scopeFactory.CreateScope();
							var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
							var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

							await DownloadQueueProcessor.RunUntilEmptyAsync(
								scopeFactory,
								env.ContentRootPath,
								ct,
								logger,
								async callbackCt => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(scopedRealtime, callbackCt));
						}
						catch (OperationCanceledException) when (ct.IsCancellationRequested)
						{
							// App is shutting down; leave remaining items queued.
						}
						catch (Exception)
						{
							// Logged by host; don't throw.
						}
					}), CancellationToken.None);
				}
				catch (Exception ex)
				{
					refreshChannelMessage = "Download Monitored failed: " + (ex.Message ?? "Unknown error");
				}
				}
			}
			else
			{
				refreshChannelMessage = "channelId is required for Download Monitored.";
			}
		}

		// GetVideoDetails: enrich existing videos with direct watch-page metadata first, then yt-dlp fallback per item.
		if (string.Equals(name, "GetVideoDetails", StringComparison.OrdinalIgnoreCase))
		{
			var detailsChannelId = 0;
			if (payload.TryGetProperty("channelId", out var dcidEl) && dcidEl.ValueKind == JsonValueKind.Number && dcidEl.TryGetInt32(out var dcid))
				detailsChannelId = dcid;

			if (detailsChannelId > 0)
			{
				try
				{
					var msg = await channelMetadataAcquisitionService.PopulateVideoMetadataAsync(db, detailsChannelId);
					refreshChannelMessage = string.IsNullOrWhiteSpace(msg)
						? "Video metadata refresh completed."
						: msg!;

					refreshStarted = DateTimeOffset.UtcNow;
					refreshEnded = DateTimeOffset.UtcNow;
				}
				catch (Exception ex)
				{
					refreshChannelMessage = "Get Video Details failed: " + (ex.Message ?? "Unknown error");
				}
			}
			else
			{
				refreshChannelMessage = "channelId is required for Get Video Details.";
			}
		}

		var body = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = !string.IsNullOrEmpty(refreshChannelMessage),
			["suppressMessages"] = string.IsNullOrEmpty(refreshChannelMessage)
		};

		if (string.Equals(name, "RefreshChannel", StringComparison.OrdinalIgnoreCase) && payload.TryGetProperty("channelId", out var cidEl))
		{
			if (cidEl.ValueKind == JsonValueKind.Number && cidEl.TryGetInt32(out var cid))
				body["channelId"] = cid;
		}

		if (string.Equals(name, "RefreshChannel", StringComparison.OrdinalIgnoreCase) && payload.TryGetProperty("channelIds", out var cidsEl) && cidsEl.ValueKind == JsonValueKind.Array)
		{
			body["channelIds"] = cidsEl
				.EnumerateArray()
				.Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
				.Select(x => x.GetInt32())
				.ToArray();
		}

		if (string.Equals(name, "DownloadMonitored", StringComparison.OrdinalIgnoreCase) && payload.TryGetProperty("channelId", out var dmCidEl))
		{
			if (dmCidEl.ValueKind == JsonValueKind.Number && dmCidEl.TryGetInt32(out var dmCid))
				body["channelId"] = dmCid;

			if (payload.TryGetProperty("playlistNumber", out var dmPnEl) && dmPnEl.ValueKind == JsonValueKind.Number && dmPnEl.TryGetInt32(out var dmPn))
				body["playlistNumber"] = dmPn;

			if (payload.TryGetProperty("videoIds", out var dmVidEl) && dmVidEl.ValueKind == JsonValueKind.Array)
			{
				var ids = dmVidEl
					.EnumerateArray()
					.Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
					.Select(x => x.GetInt32())
					.ToArray();

				if (ids.Length > 0)
					body["videoIds"] = ids;
			}
		}

		if (string.Equals(name, "GetVideoDetails", StringComparison.OrdinalIgnoreCase) && payload.TryGetProperty("channelId", out var gvdCidEl))
		{
			if (gvdCidEl.ValueKind == JsonValueKind.Number && gvdCidEl.TryGetInt32(out var gvdCid))
				body["channelId"] = gvdCid;
		}

		if (string.Equals(name, "RssSync", StringComparison.OrdinalIgnoreCase) && payload.TryGetProperty("channelId", out var rssBodyCidEl))
		{
			if (rssBodyCidEl.ValueKind == JsonValueKind.Number && rssBodyCidEl.TryGetInt32(out var rssBodyCid))
				body["channelId"] = rssBodyCid;
		}

		var command = _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "completed",
			result: "successful",
			message: refreshChannelMessage,
			queuedAt: refreshQueued,
			startedAt: refreshStarted,
			endedAt: refreshEnded);

		return command;
	}

	async Task RecordScheduledTaskIfApplicableAsync(
		string name,
		DateTimeOffset startedAt,
		DateTimeOffset completedAt,
		string? resultSummary = null)
	{
		if (!ScheduledTaskCatalog.RecordsRuns(name))
			return;

		var duration = completedAt - startedAt;
		if (duration < TimeSpan.Zero)
			duration = TimeSpan.Zero;

		await _scheduledTaskRunRecorder.RecordCompletedAsync(
			name,
			completedAt,
			duration,
			resultSummary,
			CancellationToken.None);
	}

	static bool TryGetCommandId(Dictionary<string, object?> command, out int commandId)
	{
		if (command.TryGetValue("id", out var idObj) && idObj is int parsedId)
		{
			commandId = parsedId;
			return true;
		}

		commandId = 0;
		return false;
	}

	bool IsMetadataOperationInProgressForChannel(int channelId)
	{
		return _records.AnyCommand(command =>
		{
			if (!TryGetCommandName(command, out var name) ||
			    !IsMetadataOperationCommand(name) ||
			    !TryGetCommandStatus(command, out var status) ||
			    !IsActiveCommandStatus(status))
				return false;

			if (CommandBodyTargetsChannel(command, channelId))
				return true;

			// RSS sync without channel targeting applies to monitored channels globally.
			return string.Equals(name, "RssSync", StringComparison.OrdinalIgnoreCase);
		});
	}

	static bool IsMetadataOperationCommand(string commandName)
	{
		return string.Equals(commandName, "RefreshChannel", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelUploadsPopulation", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelHydration", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelLivestreamIdentification", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelShortsParsing", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelPlaylistDiscovery", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RefreshChannelPlaylistPopulation", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "GetVideoDetails", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "GetChannelPlaylists", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(commandName, "RssSync", StringComparison.OrdinalIgnoreCase);
	}

	static bool IsActiveCommandStatus(string status)
	{
		return string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(status, "started", StringComparison.OrdinalIgnoreCase);
	}

	static bool TryGetCommandName(Dictionary<string, object?> command, out string name)
	{
		if (command.TryGetValue("name", out var nameObj) &&
		    nameObj is string parsed &&
		    !string.IsNullOrWhiteSpace(parsed))
		{
			name = parsed;
			return true;
		}

		name = string.Empty;
		return false;
	}

	static bool TryGetCommandStatus(Dictionary<string, object?> command, out string status)
	{
		if (command.TryGetValue("status", out var statusObj) &&
		    statusObj is string parsed &&
		    !string.IsNullOrWhiteSpace(parsed))
		{
			status = parsed;
			return true;
		}

		status = string.Empty;
		return false;
	}

	static bool CommandBodyTargetsChannel(Dictionary<string, object?> command, int channelId)
	{
		if (!command.TryGetValue("body", out var bodyObj) ||
		    bodyObj is not Dictionary<string, object?> body)
			return false;

		if (TryGetSingleChannelId(body, out var singleId) && singleId == channelId)
			return true;
		if (TryGetChannelIds(body, out var channelIds) && channelIds.Contains(channelId))
			return true;

		return false;
	}

	static bool TryGetSingleChannelId(Dictionary<string, object?> body, out int channelId)
	{
		channelId = 0;
		if (!body.TryGetValue("channelId", out var channelObj))
			return false;

		if (channelObj is int cid)
		{
			channelId = cid;
			return channelId > 0;
		}

		if (channelObj is JsonElement channelJson &&
			channelJson.ValueKind == JsonValueKind.Number &&
			channelJson.TryGetInt32(out var parsed))
		{
			channelId = parsed;
			return channelId > 0;
		}

		return false;
	}

	static bool TryGetChannelIds(Dictionary<string, object?> body, out HashSet<int> channelIds)
	{
		channelIds = new HashSet<int>();
		if (!body.TryGetValue("channelIds", out var channelIdsObj))
			return false;

		if (channelIdsObj is int[] intArray)
		{
			foreach (var id in intArray)
			{
				if (id > 0)
					channelIds.Add(id);
			}
			return channelIds.Count > 0;
		}

		if (channelIdsObj is JsonElement channelIdsJson && channelIdsJson.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in channelIdsJson.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var id) && id > 0)
					channelIds.Add(id);
			}
			return channelIds.Count > 0;
		}

		return false;
	}
}

