using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

	public CommandRecoveryJobRunner(
		IServiceScopeFactory scopeFactory,
		CommandRecordFactory records,
		ILogger<CommandRecoveryJobRunner> logger)
	{
		_scopeFactory = scopeFactory;
		_records = records;
		_logger = logger;
	}

	public Task ExecuteAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		if (string.Equals(workItem.JobType, CommandQueueJobTypes.RefreshChannel, StringComparison.OrdinalIgnoreCase))
			return ExecuteRefreshChannelAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.GetVideoDetails, StringComparison.OrdinalIgnoreCase))
			return ExecuteGetVideoDetailsAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.RssSync, StringComparison.OrdinalIgnoreCase))
			return ExecuteRssSyncAsync(workItem, ct);

		if (string.Equals(workItem.JobType, CommandQueueJobTypes.DownloadMonitoredQueuePump, StringComparison.OrdinalIgnoreCase))
			return ExecuteDownloadQueuePumpAsync(ct);

		throw new InvalidOperationException($"Unknown queued command job type: {workItem.JobType}");
	}

	async Task ExecuteRefreshChannelAsync(CommandQueueWorkItem workItem, CancellationToken ct)
	{
		var payload = JsonSerializer.Deserialize<RefreshChannelQueueJobPayload>(workItem.PayloadJson)
			?? throw new InvalidOperationException("Invalid refresh queue payload.");

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
				["channelIds"] = payload.ChannelIds
			});

		using var scope = _scopeFactory.CreateScope();
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
			"channelVideoListFetching",
			"Channel video list fetching",
			0,
			payload.ChannelIds.Length,
			detail: $"Fetching channel video lists for {payload.ChannelIds.Length} channel(s).",
			ct: ct);

		await progressReporter.SetStageAsync(
			"videoDetailFetching",
			"Video detail fetching",
			0,
			0,
			detail: "Waiting for discovered videos.",
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
			var maxConcurrency = payload.ChannelIds.Length <= 1 ? 1 : 3;
			using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

			var tasks = payload.ChannelIds.Select(async channelId =>
			{
				await gate.WaitAsync(ct);
				try
				{
					using var channelScope = _scopeFactory.CreateScope();
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

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = $"Refresh & Scan completed for {payload.ChannelIds.Length} channel(s).";
			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
		}
		catch (Exception ex)
		{
			var completedAt = DateTimeOffset.UtcNow;
			var failureMessage = "Refresh & Scan failed: " + (ex.Message ?? "Unknown error");
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
			var resultMessage = await scopedMetadataService.PopulateVideoMetadataAsync(
				scopedDb,
				payload.ChannelId,
				progressReporter,
				ct);

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = string.IsNullOrWhiteSpace(resultMessage)
				? "Video metadata refresh completed."
				: resultMessage!;

			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
			var completionText = await scopedRss.SyncMonitoredChannelsAsync(scopedDb, payload.ChannelId, progressReporter, ct);

			var completedAt = DateTimeOffset.UtcNow;
			var snapshot = progressReporter.GetSnapshot();
			var errorCount = snapshot.Errors.Count;

			var completionMessage = string.IsNullOrWhiteSpace(completionText)
				? "RSS sync completed."
				: completionText;

			if (errorCount > 0)
				completionMessage = $"{completionMessage} {errorCount} error(s).";

			var updated = _records.UpdateCommandRecord(command, (resource, body) =>
			{
				body["sendUpdatesToClient"] = true;
				body["suppressMessages"] = false;
				body["metadataProgress"] = _records.ToMetadataProgressResource(snapshot);
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
			throw;
		}
	}

	async Task ExecuteDownloadQueuePumpAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

		await DownloadQueueProcessor.RunUntilEmptyAsync(
			scopedDb,
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
}
