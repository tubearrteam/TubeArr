using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
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
	Task MarkCompletedAsync(long queueItemId, CancellationToken ct = default);
	Task MarkFailedAsync(long queueItemId, string error, CancellationToken ct = default);
	Task RequeueAsync(long queueItemId, string reason, CancellationToken ct = default);
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
			QueuedAtUtc = DateTimeOffset.UtcNow
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
			.Where(x => x.CommandId == commandId && x.Status == "queued")
			.OrderBy(x => x.Id)
			.FirstOrDefaultAsync(ct);

		if (job is null)
			return false;

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

		foreach (var job in runningJobs)
		{
			job.Status = "queued";
			job.StartedAtUtc = null;
			job.EndedAtUtc = null;
		}

		await db.SaveChangesAsync(ct);
	}

	public async Task<CommandQueueWorkItem?> TryDequeueAsync(CancellationToken ct = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var job = await db.CommandQueueJobs
			.Where(x => x.Status == "queued")
			.OrderBy(x => x.Id)
			.FirstOrDefaultAsync(ct);

		if (job is null)
			return null;

		job.Status = "running";
		job.StartedAtUtc = DateTimeOffset.UtcNow;
		job.EndedAtUtc = null;
		job.LastError = null;
		await db.SaveChangesAsync(ct);

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

		db.CommandQueueJobs.Remove(job);
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

		await db.SaveChangesAsync(ct);
	}
}
