using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public interface ICommandRecoveryJobRunner
{
	Task ExecuteAsync(CommandQueueWorkItem workItem, CancellationToken ct);
}

public sealed class CommandRecoveryJobRunner : ICommandRecoveryJobRunner
{
	readonly IServiceScopeFactory _scopeFactory;
	readonly CommandRecordFactory _records;
	readonly ILogger<CommandRecoveryJobRunner> _logger;
	readonly IScheduledTaskRunRecorder _scheduledTaskRunRecorder;
	readonly CommandDispatcher _dispatcher;
	readonly ICommandExecutionQueue _commandQueue;

	public CommandRecoveryJobRunner(
		IServiceScopeFactory scopeFactory,
		CommandRecordFactory records,
		ILogger<CommandRecoveryJobRunner> logger,
		IScheduledTaskRunRecorder scheduledTaskRunRecorder,
		CommandDispatcher dispatcher,
		ICommandExecutionQueue commandQueue)
	{
		_scopeFactory = scopeFactory;
		_records = records;
		_logger = logger;
		_scheduledTaskRunRecorder = scheduledTaskRunRecorder;
		_dispatcher = dispatcher;
		_commandQueue = commandQueue;
	}

	public Task ExecuteAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		if (string.Equals(workItem.JobType, CommandQueueJobTypes.RefreshChannel, StringComparison.OrdinalIgnoreCase))
			return ExecuteRefreshChannelAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.GetVideoDetails, StringComparison.OrdinalIgnoreCase))
			return ExecuteGetVideoDetailsAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.GetChannelPlaylists, StringComparison.OrdinalIgnoreCase))
			return ExecuteGetChannelPlaylistsAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.RssSync, StringComparison.OrdinalIgnoreCase))
			return ExecuteRssSyncAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.DownloadMonitoredQueuePump, StringComparison.OrdinalIgnoreCase))
			return ExecuteDownloadQueuePumpAsync(ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.RefreshMonitoredDownloads, StringComparison.OrdinalIgnoreCase))
			return ExecuteRefreshMonitoredDownloadsAsync(workItem, ct);

		throw new InvalidOperationException($"Unknown queued command job type: {workItem.JobType}");
	}

	async Task ExecuteRefreshChannelAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<RefreshChannelQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid refresh queue payload.");

		if (!workItem.CommandId.HasValue)
			throw new InvalidOperationException("Refresh queue job missing command id.");

		if (payload.Phase is null)
		{
			_ = GetOrCreateRecoveredCommand(
				workItem.CommandId,
				payload.Name,
				payload.Trigger,
				new Dictionary<string, object?>
				{
					["name"] = payload.Name,
					["trigger"] = payload.Trigger,
					["sendUpdatesToClient"] = false,
					["suppressMessages"] = true,
					["channelIds"] = payload.ChannelIds
				});
		}
		else
		{
			var phaseName = payload.Phase switch
			{
				ChannelRefreshPhase.UploadsPopulation => "RefreshChannelUploadsPopulation",
				ChannelRefreshPhase.Hydration => "RefreshChannelHydration",
				ChannelRefreshPhase.LivestreamIdentification => "RefreshChannelLivestreamIdentification",
				ChannelRefreshPhase.ShortsParsing => "RefreshChannelShortsParsing",
				ChannelRefreshPhase.PlaylistDiscovery => "RefreshChannelPlaylistDiscovery",
				ChannelRefreshPhase.PlaylistPopulation => "RefreshChannelPlaylistPopulation",
				_ => "RefreshChannel"
			};
			_ = GetOrCreateRecoveredCommand(
				workItem.CommandId,
				phaseName,
				payload.Trigger,
				new Dictionary<string, object?>
				{
					["name"] = phaseName,
					["trigger"] = payload.Trigger,
					["sendUpdatesToClient"] = false,
					["suppressMessages"] = true,
					["channelId"] = payload.ChannelIds[0],
					["channelIds"] = payload.ChannelIds,
					["metadataStep"] = payload.Phase,
					["originalCommandName"] = payload.Name
				});
		}

		await _dispatcher.ExecuteRefreshChannelWorkAsync(workItem.CommandId.Value, payload, ct);
	}

	async Task ExecuteGetVideoDetailsAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<GetVideoDetailsQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid get-video-details queue payload.");

		var command = GetOrCreateRecoveredCommand(
			workItem.CommandId,
			payload.Name,
			payload.Trigger,
			new Dictionary<string, object?>
			{
				["name"] = payload.Name,
				["trigger"] = payload.Trigger,
				["sendUpdatesToClient"] = false,
				["suppressMessages"] = true,
				["channelId"] = payload.ChannelId
			});

		using var scope = _scopeFactory.CreateScope();
		var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		var scopedMetadataService = scope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();

		var progressReporter = new MetadataProgressReporter(async (snapshot, callbackCt) =>
		{
			var updated = _records.UpdateCommandRecord(command, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, callbackCt);
		});

		await progressReporter.SetStageAsync(
			"videoDetailFetching",
			"Video detail fetching",
			0,
			0,
			detail: "Preparing video detail fetch queue.",
			ct: ct);

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
		await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = started }, ct);

		try
		{
			Func<string, Task>? reportDetails = null;
			if (workItem.CommandId is { } detailsCmdId)
			{
				reportDetails = async methodId =>
				{
					var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(detailsCmdId, methodId, ct);
					var snap = _records.UpdateCommandRecord(command, (_, body) =>
					{
						body["acquisitionMethods"] = list.ToArray();
					});
					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
				};
			}

			var resultMessage = await scopedMetadataService.PopulateVideoMetadataAsync(
				scopedDb,
				payload.ChannelId,
				progressReporter,
				ct,
				reportDetails);

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
				? "Video metadata refresh completed."
				: resultMessage!;

			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var detailsAcquisitionMethods = workItem.CommandId is { } dcid
				? await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(dcid, ct)
				: Array.Empty<string>();

			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				body["acquisitionMethods"] = detailsAcquisitionMethods.ToArray();
				resource["message"] = completionMessage;
				resource["status"] = "completed";
				resource["result"] = errorCount > 0 ? "unsuccessful" : "successful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
			await scopedRealtime.BroadcastAsync("video", new { action = "sync" }, ct);
		}
		catch (Exception ex)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var failureMessage = "Get Video Details failed: " + (ex.Message ?? "Unknown error");
			await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

			var snapshot = progressReporter.GetSnapshot();
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				resource["message"] = failureMessage;
				resource["status"] = "failed";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
			throw;
		}
	}

	async Task ExecuteGetChannelPlaylistsAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<GetChannelPlaylistsQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid get-channel-playlists queue payload.");

		var command = GetOrCreateRecoveredCommand(
			workItem.CommandId,
			payload.Name,
			payload.Trigger,
			new Dictionary<string, object?>
			{
				["name"] = payload.Name,
				["trigger"] = payload.Trigger,
				["sendUpdatesToClient"] = false,
				["suppressMessages"] = true,
				["channelId"] = payload.ChannelId
			});

		using var scope = _scopeFactory.CreateScope();
		var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		var scopedPlaylistService = scope.ServiceProvider.GetRequiredService<ChannelPlaylistDiscoveryService>();

		var progressReporter = new MetadataProgressReporter(async (snapshot, callbackCt) =>
		{
			var updated = _records.UpdateCommandRecord(command, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, callbackCt);
		});

		await progressReporter.SetStageAsync(
			"playlistDiscovery",
			"Playlist discovery",
			0,
			0,
			detail: "Preparing playlist fetch…",
			ct: ct);

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
		await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = started }, ct);

		try
		{
			Func<string, Task>? reportMethod = null;
			if (workItem.CommandId is { } playlistsCmdId)
			{
				reportMethod = async methodId =>
				{
					var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(playlistsCmdId, methodId, ct);
					var snap = _records.UpdateCommandRecord(command, (_, body) =>
					{
						body["acquisitionMethods"] = list.ToArray();
					});
					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
				};
			}

			var resultMessage = await scopedPlaylistService.FetchAndUpsertPlaylistsAsync(
				scopedDb,
				payload.ChannelId,
				progressReporter,
				ct,
				reportMethod);

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
				? "Playlists updated."
				: resultMessage!;

			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var playlistsAcquisitionMethods = workItem.CommandId is { } plCid
				? await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(plCid, ct)
				: Array.Empty<string>();

			var unsuccessful = errorCount > 0 || !string.IsNullOrWhiteSpace(resultMessage);
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				body["acquisitionMethods"] = playlistsAcquisitionMethods.ToArray();
				resource["message"] = completionMessage;
				resource["status"] = "completed";
				resource["result"] = unsuccessful ? "unsuccessful" : "successful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
			await scopedRealtime.BroadcastAsync("channel", new { action = "sync" }, ct);
		}
		catch (Exception ex)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var failureMessage = "Get Playlists failed: " + (ex.Message ?? "Unknown error");
			await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

			var snapshot = progressReporter.GetSnapshot();
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				resource["message"] = failureMessage;
				resource["status"] = "failed";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);
			throw;
		}
	}

	async Task ExecuteRssSyncAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<RssSyncQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid RSS sync queue payload.");

		var command = GetOrCreateRecoveredCommand(
			workItem.CommandId,
			payload.Name,
			payload.Trigger,
			new Dictionary<string, object?>
			{
				["name"] = payload.Name,
				["trigger"] = payload.Trigger,
				["sendUpdatesToClient"] = false,
				["suppressMessages"] = true,
				["channelId"] = payload.ChannelId
			});

		using var scope = _scopeFactory.CreateScope();
		var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var scopedRss = scope.ServiceProvider.GetRequiredService<ChannelRssSyncService>();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

		var progressReporter = new MetadataProgressReporter(async (snapshot, callbackCt) =>
		{
			var updated = _records.UpdateCommandRecord(command, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, callbackCt);
		});

		await progressReporter.SetStageAsync(
			"rssFeedSync",
			"RSS feed sync",
			0,
			0,
			detail: "Preparing RSS sync...",
			ct: ct);

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
		await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = started }, ct);

		try
		{
			Func<string, Task>? reportMethod = null;
			if (workItem.CommandId is { } rssCmdId)
			{
				reportMethod = async methodId =>
				{
					var list = await _commandQueue.MergeAcquisitionMethodByCommandIdAsync(rssCmdId, methodId, ct);
					var snap = _records.UpdateCommandRecord(command, (_, body) =>
					{
						body["acquisitionMethods"] = list.ToArray();
					});
					await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = snap }, ct);
				};
			}

			var completionText = await scopedRss.SyncMonitoredChannelsAsync(scopedDb, payload.ChannelId, progressReporter, ct, reportMethod);

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = string.IsNullOrWhiteSpace(completionText)
				? "RSS sync completed."
				: completionText;

			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var rssAcquisitionMethods = workItem.CommandId is { } rssCid
				? await _commandQueue.GetAcquisitionMethodsByCommandIdAsync(rssCid, ct)
				: Array.Empty<string>();

			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				body["acquisitionMethods"] = rssAcquisitionMethods.ToArray();
				resource["message"] = completionMessage;
				resource["status"] = "completed";
				resource["result"] = errorCount > 0 ? "unsuccessful" : "successful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
			await scopedRealtime.BroadcastAsync("channel", new { action = "sync" }, ct);
			await scopedRealtime.BroadcastAsync("video", new { action = "sync" }, ct);

			if (ScheduledTaskCatalog.RecordsRuns(payload.Name))
			{
				var dur = completedAt - startedAt;
				if (dur < TimeSpan.Zero)
					dur = TimeSpan.Zero;
				await _scheduledTaskRunRecorder.RecordCompletedAsync(
					payload.Name,
					completedAt,
					dur,
					completionMessage,
					ct);
			}
		}
		catch (Exception ex)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var failureMessage = "RSS sync failed: " + (ex.Message ?? "Unknown error");
			await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

			var snapshot = progressReporter.GetSnapshot();
			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
				resource["message"] = failureMessage;
				resource["status"] = "failed";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, CancellationToken.None);

			if (ScheduledTaskCatalog.RecordsRuns(payload.Name))
			{
				var dur = completedAt - startedAt;
				if (dur < TimeSpan.Zero)
					dur = TimeSpan.Zero;
				await _scheduledTaskRunRecorder.RecordCompletedAsync(
					payload.Name,
					completedAt,
					dur,
					failureMessage,
					ct);
			}

			throw;
		}
	}

	async Task ExecuteRefreshMonitoredDownloadsAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<RefreshMonitoredDownloadsQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid RefreshMonitoredDownloads queue payload.");

		var command = GetOrCreateRecoveredCommand(
			workItem.CommandId,
			payload.Name,
			payload.Trigger,
			new Dictionary<string, object?>
			{
				["name"] = payload.Name,
				["trigger"] = payload.Trigger,
				["sendUpdatesToClient"] = false,
				["suppressMessages"] = true,
			});

		using var scope = _scopeFactory.CreateScope();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

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
		await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = started }, ct);

		try
		{
			using var innerScope = _scopeFactory.CreateScope();
			var scopedDb = innerScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var innerRealtime = innerScope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
			var logger = innerScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

			var monitoredChannelIds = await scopedDb.Channels.AsNoTracking()
				.Where(c => c.Monitored)
				.Select(c => c.Id)
				.ToListAsync(ct);
			var metadataBusyChannelIds = new HashSet<int>();
			foreach (var channelId in monitoredChannelIds)
			{
				if (IsMetadataOperationInProgressForChannel(channelId))
					metadataBusyChannelIds.Add(channelId);
			}

			var enqueuedMissing = await DownloadQueueProcessor.EnqueueMonitoredMissingOnDiskAsync(
				scopedDb,
				metadataBusyChannelIds,
				ct,
				logger);
			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(innerRealtime, ct);

			var env = innerScope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
			await DownloadQueueProcessor.RunUntilEmptyAsync(
				_scopeFactory,
				env.ContentRootPath,
				ct,
				logger,
				async callbackCt => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(innerRealtime, callbackCt));

			var completedAt = DateTimeOffset.UtcNow;
			var enqueueSummary = enqueuedMissing > 0
				? $"Queued {enqueuedMissing} monitored video(s) with no file on disk; download queue processed."
				: "No monitored videos missing on-disk files; download queue processed.";
			var summary = metadataBusyChannelIds.Count > 0
				? $"{enqueueSummary} Skipped {metadataBusyChannelIds.Count} channel(s) with active metadata operations."
				: enqueueSummary;

			var updated = _records.UpdateCommandRecord(command, (resource, _) =>
			{
				resource["message"] = summary;
				resource["status"] = "completed";
				resource["result"] = "successful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await innerRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);

			if (ScheduledTaskCatalog.RecordsRuns(payload.Name))
			{
				var duration = completedAt - startedAt;
				if (duration < TimeSpan.Zero)
					duration = TimeSpan.Zero;
				await _scheduledTaskRunRecorder.RecordCompletedAsync(
					payload.Name,
					completedAt,
					duration,
					summary,
					ct);
			}
		}
		catch (Exception ex)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var failureMessage = "Download queue processing failed: " + (ex.Message ?? "Unknown error");
			var updated = _records.UpdateCommandRecord(command, (resource, _) =>
			{
				resource["message"] = failureMessage;
				resource["status"] = "failed";
				resource["result"] = "unsuccessful";
				resource["started"] = startedAt.ToString("O");
				resource["ended"] = completedAt.ToString("O");
				resource["duration"] = CommandRecordFactory.FormatCommandDuration(completedAt - startedAt);
				resource["stateChangeTime"] = completedAt.ToString("O");
				resource["lastExecutionTime"] = completedAt.ToString("O");
			});

			await scopedRealtime.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);

			if (ScheduledTaskCatalog.RecordsRuns(payload.Name))
			{
				var duration = completedAt - startedAt;
				if (duration < TimeSpan.Zero)
					duration = TimeSpan.Zero;
				await _scheduledTaskRunRecorder.RecordCompletedAsync(
					payload.Name,
					completedAt,
					duration,
					failureMessage,
					ct);
			}

			throw;
		}
	}

	async Task ExecuteDownloadQueuePumpAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
		var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

		await DownloadQueueProcessor.RunUntilEmptyAsync(
			_scopeFactory,
			env.ContentRootPath,
			ct,
			logger,
			async callbackCt => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(scopedRealtime, callbackCt));
	}

	Dictionary<string, object?> GetOrCreateRecoveredCommand(
		int? commandId,
		string name,
		string trigger,
		Dictionary<string, object?> body)
	{
		if (commandId is { } existingId && _records.TryGetCommandById(existingId, out var existingCommand))
			return existingCommand;

		var now = DateTimeOffset.UtcNow;
		return _records.CreateCommandRecord(
			name,
			trigger,
			body,
			status: "queued",
			result: "unknown",
			message: "Recovered queued job after restart.",
			queuedAt: now,
			startedAt: now,
			endedAt: now);
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
