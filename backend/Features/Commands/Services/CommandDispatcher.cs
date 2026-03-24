using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TubeArr.Backend.Data;
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

	public CommandDispatcher(CommandRecordFactory records, ICommandExecutionQueue commandQueue)
	{
		_records = records;
		_commandQueue = commandQueue;
	}

	public Task<Dictionary<string, object?>> QueueRefreshChannelAsync(
		int channelId,
		string trigger,
		IServiceScopeFactory scopeFactory,
		IRealtimeEventBroadcaster realtime)
	{
		return QueueRefreshChannelsAsync([channelId], "RefreshChannel", trigger, scopeFactory, realtime);
	}

	public async Task<Dictionary<string, object?>> DispatchAsync(
		JsonElement payload,
		TubeArrDbContext db,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		IRealtimeEventBroadcaster realtime,
		ChannelMetadataAcquisitionService channelMetadataAcquisitionService)
	{
		var name = payload.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
			? nameEl.GetString() ?? ""
			: "";

		var trigger = payload.TryGetProperty("trigger", out var triggerEl) && triggerEl.ValueKind == JsonValueKind.String
			? triggerEl.GetString() ?? "manual"
			: "manual";

		if (string.Equals(name, "RefreshChannel", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareRefreshChannelAsync(payload, name, trigger, scopeFactory, realtime);

		if (string.Equals(name, "GetVideoDetails", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareGetVideoDetailsAsync(payload, name, trigger, db, scopeFactory, logger, realtime, channelMetadataAcquisitionService);

		if (string.Equals(name, "RssSync", StringComparison.OrdinalIgnoreCase))
			return await HandleProgressAwareRssSyncAsync(payload, name, trigger, db, scopeFactory, logger, realtime);

		if (string.Equals(name, "ResetApiKey", StringComparison.OrdinalIgnoreCase))
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			serverSettings.ApiKey = ProgramStartupHelpers.GenerateApiKey();
			await db.SaveChangesAsync();
		}

		return await HandleLegacyCommandAsync(payload, name, trigger, db, scopeFactory, logger, realtime, channelMetadataAcquisitionService);
	}

	async Task<Dictionary<string, object?>> HandleProgressAwareRefreshChannelAsync(
		JsonElement payload,
		string name,
		string trigger,
		IServiceScopeFactory scopeFactory,
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

		return await QueueRefreshChannelsAsync(channelIds, name, trigger, scopeFactory, realtime);
	}

	async Task<Dictionary<string, object?>> QueueRefreshChannelsAsync(
		IEnumerable<int> channelIds,
		string name,
		string trigger,
		IServiceScopeFactory scopeFactory,
		IRealtimeEventBroadcaster realtime)
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

		Dictionary<string, object?>? refreshCommand = null;
		IRealtimeEventBroadcaster progressBroadcaster = realtime;

		var progressReporter = new MetadataProgressReporter(async (snapshot, ct) =>
		{
			if (refreshCommand is null)
				return;

			var updated = _records.UpdateCommandRecord(refreshCommand, (_, body) =>
			{
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
			});

			await progressBroadcaster.BroadcastAsync("command", new { action = "updated", resource = updated }, ct);
		});

		await progressReporter.SetStageAsync(
			"channelVideoListFetching",
			"Channel video list fetching",
			0,
			normalizedChannelIds.Count,
			detail: $"Fetching channel video lists for {normalizedChannelIds.Count} channel(s).",
			ct: CancellationToken.None);

		await progressReporter.SetStageAsync(
			"videoDetailFetching",
			"Video detail fetching",
			0,
			0,
			detail: "Waiting for discovered videos.",
			ct: CancellationToken.None);

		var refreshQueuedAt = DateTimeOffset.UtcNow;
		var refreshBody = new Dictionary<string, object?>
		{
			["name"] = name,
			["trigger"] = trigger,
			["sendUpdatesToClient"] = false,
			["suppressMessages"] = true,
			["channelIds"] = normalizedChannelIds.ToArray(),
			["metadataProgress"] = _records.ToMetadataProgressResource(progressReporter.GetSnapshot())
		};
		if (normalizedChannelIds.Count == 1)
			refreshBody["channelId"] = normalizedChannelIds[0];

		refreshCommand = _records.CreateCommandRecord(
			name,
			trigger,
			refreshBody,
			status: "queued",
			result: "unknown",
			message: "",
			queuedAt: refreshQueuedAt,
			startedAt: refreshQueuedAt,
			endedAt: refreshQueuedAt);

		await _records.BroadcastCommandUpdateAsync(realtime, refreshCommand);

		if (TryGetCommandId(refreshCommand, out var refreshCommandId))
		{
			var refreshQueuePayload = JsonSerializer.Serialize(new RefreshChannelQueueJobPayload(name, trigger, normalizedChannelIds.ToArray()));
			await _commandQueue.EnqueueAsync(new CommandQueueWorkItem(refreshCommandId, name, CommandQueueJobTypes.RefreshChannel, refreshQueuePayload, async ct =>
			{
				var startedAt = DateTimeOffset.UtcNow;
				var completedAt = startedAt;

				try
				{
					var started = _records.UpdateCommandRecord(refreshCommand, (command, _) =>
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
					var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
					progressBroadcaster = scopedRealtime;

					var maxConcurrency = normalizedChannelIds.Count <= 1 ? 1 : 3;
					using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

					var tasks = normalizedChannelIds.Select(async channelId =>
					{
						await gate.WaitAsync(ct);
						try
						{
							using var channelScope = scopeFactory.CreateScope();
							var channelDb = channelScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
							var channelMetadataService = channelScope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();

							var resultMessage = await channelMetadataService.PopulateChannelDetailsAsync(
								channelDb,
								channelId,
								progressReporter,
								ct);

							if (!string.IsNullOrWhiteSpace(resultMessage))
								await progressReporter.AddErrorAsync(resultMessage, ct);
						}
						catch (Exception ex)
						{
							await progressReporter.AddErrorAsync(
								$"Channel {channelId}: Refresh & Scan failed: " + (ex.Message ?? "Unknown error"),
								ct);
						}
						finally
						{
							gate.Release();
						}
					}).ToArray();

					await Task.WhenAll(tasks);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = $"Refresh & Scan completed for {normalizedChannelIds.Count} channel(s).";
					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var updated = _records.UpdateCommandRecord(refreshCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					completedAt = DateTimeOffset.UtcNow;
					var abortedMessage = "Refresh & Scan was aborted during shutdown.";
					await progressReporter.AddErrorAsync(abortedMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(refreshCommand, (command, body) =>
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
					var failureMessage = "Refresh & Scan failed: " + (ex.Message ?? "Unknown error");
					await progressReporter.AddErrorAsync(failureMessage, CancellationToken.None);

					var snapshot = progressReporter.GetSnapshot();
					var updated = _records.UpdateCommandRecord(refreshCommand, (command, body) =>
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

		return _records.SnapshotCommand(refreshCommand);
	}

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

					var resultMessage = await scopedMetadataService.PopulateVideoMetadataAsync(
						scopedDb,
						detailsChannelId,
						progressReporter,
						ct);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
						? "Video metadata refresh completed."
						: resultMessage!;

					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var updated = _records.UpdateCommandRecord(detailsCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
			detail: "Preparing RSS syncâ€¦");

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

					var completionText = await scopedRss.SyncMonitoredChannelsAsync(scopedDb, rssOnlyChannelId, progressReporter, ct);

					completedAt = DateTimeOffset.UtcNow;
					var snapshot = progressReporter.GetSnapshot();
					var errorCount = snapshot.Errors.Count;

					var completionMessage = string.IsNullOrWhiteSpace(completionText)
						? "RSS sync completed."
						: completionText;

					if (errorCount > 0)
						completionMessage = $"{completionMessage} {errorCount} error(s).";

					var updated = _records.UpdateCommandRecord(rssCommand, (command, body) =>
					{
						body["sendUpdatesToClient"] = true;
						body["suppressMessages"] = false;
						body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
				}
			}), CancellationToken.None);
		}

		return _records.SnapshotCommand(rssCommand);
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
						var msg = await channelMetadataAcquisitionService.PopulateChannelDetailsAsync(db, channelId);
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
							var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
							var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

							await DownloadQueueProcessor.RunUntilEmptyAsync(
								scopedDb,
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
}

