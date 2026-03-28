using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public interface ICommandExecutionQueue
{
	Task EnqueueAsync(CommandQueueWorkItem workItem, CancellationToken ct = default);
	Task<bool> TryCancelAsync(int commandId, CancellationToken ct = default);
	Task RecoverRunningJobsAsync(CancellationToken ct = default);
	Task<CommandQueueWorkItem?> TryDequeueAsync(CancellationToken ct = default);
	/// <summary>Marks the job completed and retains the row in <c>CommandQueueJobs</c> for inspection (status <c>completed</c>, <c>EndedAtUtc</c> set).</summary>
	Task MarkCompletedAsync(long queueItemId, CancellationToken ct = default);
	Task MarkFailedAsync(long queueItemId, string error, CancellationToken ct = default);
	Task RequeueAsync(long queueItemId, string reason, CancellationToken ct = default);
	/// <summary>Merges a method id into the active <see cref="CommandQueueJobEntity"/> for this command and returns the merged list.</summary>
	Task<IReadOnlyList<string>> MergeAcquisitionMethodByCommandIdAsync(int commandId, string methodId, CancellationToken ct = default);
	Task<IReadOnlyList<string>> GetAcquisitionMethodsByCommandIdAsync(int commandId, CancellationToken ct = default);
}

public sealed class CommandQueueWorkItem
{
	public CommandQueueWorkItem(
		int? commandId,
		string name,
		string jobType,
		string payloadJson,
		Func<CancellationToken, Task>? executeAsync = null)
		: this(0, commandId, name, jobType, payloadJson, executeAsync)
	{
	}

	CommandQueueWorkItem(
		long queueItemId,
		int? commandId,
		string name,
		string jobType,
		string payloadJson,
		Func<CancellationToken, Task>? executeAsync)
	{
		QueueItemId = queueItemId;
		CommandId = commandId;
		Name = name;
		JobType = jobType;
		PayloadJson = payloadJson;
		ExecuteAsync = executeAsync;
	}

	public long QueueItemId { get; }
	public int? CommandId { get; }
	public string Name { get; }
	public string JobType { get; }
	public string PayloadJson { get; }
	public Func<CancellationToken, Task>? ExecuteAsync { get; }

	public CommandQueueWorkItem WithQueueItemId(long queueItemId)
	{
		return new CommandQueueWorkItem(queueItemId, CommandId, Name, JobType, PayloadJson, ExecuteAsync);
	}
}

public sealed class InProcessCommandExecutionQueue : ICommandExecutionQueue
{
	readonly IServiceScopeFactory _scopeFactory;
	readonly ConcurrentDictionary<long, Func<CancellationToken, Task>> _inlineHandlers = new();

	public InProcessCommandExecutionQueue(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task EnqueueAsync(CommandQueueWorkItem workItem, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var entity = new CommandQueueJobEntity
		{
			CommandId = workItem.CommandId,
			Name = workItem.Name,
			JobType = workItem.JobType,
			PayloadJson = workItem.PayloadJson,
			Status = "queued",
			QueuedAtUtc = DateTimeOffset.UtcNow,
			AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.DefaultCommandJson
		};

		db.CommandQueueJobs.Add(entity);
		await db.SaveChangesAsync(ct);

		if (IsMetadataQueueJobType(workItem.JobType))
		{
			db.MetadataQueue.Add(new MetadataQueueEntity
			{
				CommandQueueJobId = entity.Id,
				CommandId = workItem.CommandId,
				ChannelId = TryGetChannelIdFromMetadataPayload(workItem.PayloadJson),
				Name = workItem.Name,
				JobType = workItem.JobType,
				PayloadJson = workItem.PayloadJson,
				Status = QueueJobStatuses.Queued,
				QueuedAtUtc = entity.QueuedAtUtc,
				AcquisitionMethodsJson = entity.AcquisitionMethodsJson
			});
			await db.SaveChangesAsync(ct);
		}

		if (workItem.ExecuteAsync is not null)
			_inlineHandlers[entity.Id] = workItem.ExecuteAsync;
	}

	public async Task<bool> TryCancelAsync(int commandId, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		// Include "running": after TryDequeueAsync the row is no longer "queued", so a narrow queued-only cancel
		// always failed with 409 while the UI still showed the command as active (metadata / clear queue).
		var job = await db.CommandQueueJobs
			.Where(x => x.CommandId == commandId && (x.Status == "queued" || x.Status == "running"))
			.OrderBy(x => x.Id)
			.FirstOrDefaultAsync(ct);

		if (job is null)
			return false;

		var meta = await db.MetadataQueue.FirstOrDefaultAsync(x => x.CommandQueueJobId == job.Id, ct);
		if (meta is not null)
		{
			db.MetadataHistory.Add(new MetadataHistoryEntity
			{
				CommandQueueJobId = meta.CommandQueueJobId,
				CommandId = meta.CommandId,
				ChannelId = meta.ChannelId,
				Name = meta.Name,
				JobType = meta.JobType,
				PayloadJson = meta.PayloadJson,
				ResultStatus = QueueJobStatuses.Aborted,
				Message = "Cancelled.",
				QueuedAtUtc = meta.QueuedAtUtc,
				StartedAtUtc = meta.StartedAtUtc,
				EndedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = meta.AcquisitionMethodsJson
			});
			db.MetadataQueue.Remove(meta);
		}

		db.CommandQueueJobs.Remove(job);
		await db.SaveChangesAsync(ct);

		_inlineHandlers.TryRemove(job.Id, out _);
		return true;
	}

	public async Task RecoverRunningJobsAsync(CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var runningJobs = await db.CommandQueueJobs
			.Where(x => x.Status == "running")
			.ToListAsync(ct);

		if (runningJobs.Count == 0)
			return;

		var jobIds = runningJobs.Select(j => j.Id).ToList();
		var metas = await db.MetadataQueue
			.Where(m => jobIds.Contains(m.CommandQueueJobId))
			.ToListAsync(ct);

		foreach (var job in runningJobs)
		{
			job.Status = "queued";
			job.StartedAtUtc = null;
			job.EndedAtUtc = null;
		}

		foreach (var m in metas)
		{
			m.Status = QueueJobStatuses.Queued;
			m.StartedAtUtc = null;
			m.EndedAtUtc = null;
		}

		await db.SaveChangesAsync(ct);
	}

	public async Task<CommandQueueWorkItem?> TryDequeueAsync(CancellationToken ct = default)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		CommandQueueJobEntity? job;
		await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
		{
			job = await db.CommandQueueJobs
				.Where(x => x.Status == "queued")
				.OrderBy(x => x.Id)
				.FirstOrDefaultAsync(ct);

			if (job is null)
			{
				await tx.CommitAsync(ct);
				return null;
			}

			job.Status = "running";
			job.StartedAtUtc = DateTimeOffset.UtcNow;
			job.EndedAtUtc = null;
			job.LastError = null;

			var meta = await db.MetadataQueue.FirstOrDefaultAsync(x => x.CommandQueueJobId == job.Id, ct);
			if (meta is not null)
			{
				meta.Status = QueueJobStatuses.Running;
				meta.StartedAtUtc = job.StartedAtUtc;
			}

			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		}

		var executeAsync = _inlineHandlers.TryGetValue(job.Id, out var handler)
			? handler
			: null;

		return new CommandQueueWorkItem(job.CommandId, job.Name, job.JobType, job.PayloadJson, executeAsync)
			.WithQueueItemId(job.Id);
	}

	public async Task MarkCompletedAsync(long queueItemId, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs.FirstOrDefaultAsync(x => x.Id == queueItemId, ct);
		if (job is null)
		{
			_inlineHandlers.TryRemove(queueItemId, out _);
			return;
		}

		job.Status = QueueJobStatuses.Completed;
		job.EndedAtUtc = DateTimeOffset.UtcNow;
		job.LastError = null;

		var metaDone = await db.MetadataQueue.FirstOrDefaultAsync(x => x.CommandQueueJobId == queueItemId, ct);
		if (metaDone is not null)
		{
			db.MetadataHistory.Add(new MetadataHistoryEntity
			{
				CommandQueueJobId = metaDone.CommandQueueJobId,
				CommandId = metaDone.CommandId,
				ChannelId = metaDone.ChannelId,
				Name = metaDone.Name,
				JobType = metaDone.JobType,
				PayloadJson = metaDone.PayloadJson,
				ResultStatus = QueueJobStatuses.Completed,
				Message = null,
				QueuedAtUtc = metaDone.QueuedAtUtc,
				StartedAtUtc = metaDone.StartedAtUtc,
				EndedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = metaDone.AcquisitionMethodsJson
			});
			db.MetadataQueue.Remove(metaDone);
		}

		await db.SaveChangesAsync(ct);
		_inlineHandlers.TryRemove(queueItemId, out _);
	}

	public async Task MarkFailedAsync(long queueItemId, string error, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs.FirstOrDefaultAsync(x => x.Id == queueItemId, ct);
		if (job is null)
		{
			_inlineHandlers.TryRemove(queueItemId, out _);
			return;
		}

		job.Status = "failed";
		job.LastError = string.IsNullOrWhiteSpace(error) ? "Unknown queue execution failure." : error;
		job.EndedAtUtc = DateTimeOffset.UtcNow;

		var metaFail = await db.MetadataQueue.FirstOrDefaultAsync(x => x.CommandQueueJobId == queueItemId, ct);
		if (metaFail is not null)
		{
			db.MetadataHistory.Add(new MetadataHistoryEntity
			{
				CommandQueueJobId = metaFail.CommandQueueJobId,
				CommandId = metaFail.CommandId,
				ChannelId = metaFail.ChannelId,
				Name = metaFail.Name,
				JobType = metaFail.JobType,
				PayloadJson = metaFail.PayloadJson,
				ResultStatus = QueueJobStatuses.Failed,
				Message = job.LastError,
				QueuedAtUtc = metaFail.QueuedAtUtc,
				StartedAtUtc = metaFail.StartedAtUtc,
				EndedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = metaFail.AcquisitionMethodsJson
			});
			db.MetadataQueue.Remove(metaFail);
		}

		await db.SaveChangesAsync(ct);
		_inlineHandlers.TryRemove(queueItemId, out _);
	}

	public async Task RequeueAsync(long queueItemId, string reason, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs.FirstOrDefaultAsync(x => x.Id == queueItemId, ct);
		if (job is null)
			return;

		job.Status = "queued";
		job.StartedAtUtc = null;
		job.EndedAtUtc = null;
		job.LastError = reason;

		var metaRe = await db.MetadataQueue.FirstOrDefaultAsync(x => x.CommandQueueJobId == queueItemId, ct);
		if (metaRe is not null)
		{
			metaRe.Status = QueueJobStatuses.Queued;
			metaRe.StartedAtUtc = null;
			metaRe.EndedAtUtc = null;
			metaRe.LastError = reason;
		}

		await db.SaveChangesAsync(ct);
	}

	public async Task<IReadOnlyList<string>> MergeAcquisitionMethodByCommandIdAsync(int commandId, string methodId, CancellationToken ct = default)
	{
		if (commandId <= 0 || string.IsNullOrWhiteSpace(methodId))
			return Array.Empty<string>();

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var job = await db.CommandQueueJobs
			.Where(x => x.CommandId == commandId)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync(ct);
		if (job is null)
			return Array.Empty<string>();

		job.AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.MergeOne(job.AcquisitionMethodsJson, methodId.Trim());

		var metaMerge = await db.MetadataQueue
			.Where(x => x.CommandId == commandId && (x.Status == QueueJobStatuses.Queued || x.Status == QueueJobStatuses.Running))
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync(ct);
		if (metaMerge is not null)
			metaMerge.AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.MergeOne(metaMerge.AcquisitionMethodsJson, methodId.Trim());

		await db.SaveChangesAsync(ct);
		return AcquisitionMethodsJsonHelper.Parse(job.AcquisitionMethodsJson);
	}

	public async Task<IReadOnlyList<string>> GetAcquisitionMethodsByCommandIdAsync(int commandId, CancellationToken ct = default)
	{
		if (commandId <= 0)
			return Array.Empty<string>();

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var job = await db.CommandQueueJobs
			.Where(x => x.CommandId == commandId)
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync(ct);
		if (job is null)
			return Array.Empty<string>();

		return AcquisitionMethodsJsonHelper.Parse(job.AcquisitionMethodsJson);
	}

	static bool IsMetadataQueueJobType(string jobType) =>
		string.Equals(jobType, CommandQueueJobTypes.RefreshChannel, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.GetVideoDetails, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.GetChannelPlaylists, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(jobType, CommandQueueJobTypes.RssSync, StringComparison.OrdinalIgnoreCase);

	static int? TryGetChannelIdFromMetadataPayload(string? payloadJson)
	{
		if (string.IsNullOrWhiteSpace(payloadJson))
			return null;
		try
		{
			using var doc = JsonDocument.Parse(payloadJson);
			var root = doc.RootElement;
			if (root.TryGetProperty("ChannelIds", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
			{
				var first = arr[0];
				if (first.ValueKind == JsonValueKind.Number)
					return first.GetInt32();
			}
			if (root.TryGetProperty("channelId", out var c) && c.ValueKind == JsonValueKind.Number)
				return c.GetInt32();
			if (root.TryGetProperty("ChannelId", out var c2) && c2.ValueKind == JsonValueKind.Number)
				return c2.GetInt32();
		}
		catch
		{
		}
		return null;
	}
}
