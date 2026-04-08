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
	const int MaxTerminalCommandJobs = 5000;

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
			Category = CommandQueueJobCategoryResolver.FromJobType(workItem.JobType),
			ChannelId = TryGetChannelIdFromQueuePayload(workItem.PayloadJson),
			PayloadJson = workItem.PayloadJson,
			Status = QueueJobStatuses.Queued,
			QueuedAtUtc = DateTimeOffset.UtcNow,
			AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.DefaultCommandJson
		};

		db.CommandQueueJobs.Add(entity);
		await db.SaveChangesAsync(ct);

		if (workItem.ExecuteAsync is not null)
			_inlineHandlers[entity.Id] = workItem.ExecuteAsync;
	}

	public async Task<bool> TryCancelAsync(int commandId, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs
			.Where(x => x.CommandId == commandId && (x.Status == QueueJobStatuses.Queued || x.Status == QueueJobStatuses.Running))
			.OrderBy(x => x.Id)
			.FirstOrDefaultAsync(ct);

		if (job is null)
			return false;

		job.Status = QueueJobStatuses.Aborted;
		job.EndedAtUtc = DateTimeOffset.UtcNow;
		job.LastError = "Cancelled.";

		await db.SaveChangesAsync(ct);
		await TrimTerminalCommandJobsIfNeededAsync(db, ct);

		_inlineHandlers.TryRemove(job.Id, out _);
		return true;
	}

	public async Task RecoverRunningJobsAsync(CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var runningJobs = await db.CommandQueueJobs
			.Where(x => x.Status == QueueJobStatuses.Running)
			.ToListAsync(ct);

		if (runningJobs.Count == 0)
			return;

		foreach (var job in runningJobs)
		{
			job.Status = QueueJobStatuses.Queued;
			job.StartedAtUtc = null;
			job.EndedAtUtc = null;
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
				.Where(x => x.Status == QueueJobStatuses.Queued)
				.OrderBy(x => x.Id)
				.FirstOrDefaultAsync(ct);

			if (job is null)
			{
				await tx.CommitAsync(ct);
				return null;
			}

			job.Status = QueueJobStatuses.Running;
			job.StartedAtUtc = DateTimeOffset.UtcNow;
			job.EndedAtUtc = null;
			job.LastError = null;

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

		await db.SaveChangesAsync(ct);
		await TrimTerminalCommandJobsIfNeededAsync(db, ct);
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

		job.Status = QueueJobStatuses.Failed;
		job.LastError = string.IsNullOrWhiteSpace(error) ? "Unknown queue execution failure." : error;
		job.EndedAtUtc = DateTimeOffset.UtcNow;

		await db.SaveChangesAsync(ct);
		await TrimTerminalCommandJobsIfNeededAsync(db, ct);
		_inlineHandlers.TryRemove(queueItemId, out _);
	}

	public async Task RequeueAsync(long queueItemId, string reason, CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs.FirstOrDefaultAsync(x => x.Id == queueItemId, ct);
		if (job is null)
			return;

		job.Status = QueueJobStatuses.Queued;
		job.StartedAtUtc = null;
		job.EndedAtUtc = null;
		job.LastError = reason;

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

	static int? TryGetChannelIdFromQueuePayload(string? payloadJson)
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

	static async Task TrimTerminalCommandJobsIfNeededAsync(TubeArrDbContext db, CancellationToken ct)
	{
		var terminalCount = await db.CommandQueueJobs.CountAsync(x =>
			x.Status == QueueJobStatuses.Completed || x.Status == QueueJobStatuses.Failed || x.Status == QueueJobStatuses.Aborted, ct);
		if (terminalCount <= MaxTerminalCommandJobs)
			return;

		var cutoff = await db.CommandQueueJobs
			.Where(x => x.Status == QueueJobStatuses.Completed || x.Status == QueueJobStatuses.Failed || x.Status == QueueJobStatuses.Aborted)
			.OrderByDescending(x => x.Id)
			.Select(x => x.Id)
			.Skip(MaxTerminalCommandJobs - 1)
			.FirstAsync(ct);

		await db.CommandQueueJobs.Where(x =>
				(x.Status == QueueJobStatuses.Completed || x.Status == QueueJobStatuses.Failed || x.Status == QueueJobStatuses.Aborted)
				&& x.Id < cutoff)
			.ExecuteDeleteAsync(ct);
	}
}
