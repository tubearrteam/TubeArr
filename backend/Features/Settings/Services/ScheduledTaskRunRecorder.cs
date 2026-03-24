using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public interface IScheduledTaskRunRecorder
{
	Task RecordCompletedAsync(
		string taskName,
		DateTimeOffset completedAt,
		TimeSpan duration,
		string? resultMessage = null,
		CancellationToken ct = default);
}

public sealed class ScheduledTaskRunRecorder : IScheduledTaskRunRecorder
{
	readonly IServiceScopeFactory _scopeFactory;

	public ScheduledTaskRunRecorder(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task RecordCompletedAsync(
		string taskName,
		DateTimeOffset completedAt,
		TimeSpan duration,
		string? resultMessage = null,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(taskName))
			return;

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var row = await db.ScheduledTaskStates.FirstOrDefaultAsync(x => x.TaskName == taskName, ct);
		if (row is null)
		{
			row = new ScheduledTaskStateEntity { TaskName = taskName };
			db.ScheduledTaskStates.Add(row);
		}

		row.LastCompletedAt = completedAt;
		row.LastDurationTicks = duration.Ticks;

		db.ScheduledTaskRunHistory.Add(new ScheduledTaskRunHistoryEntity
		{
			TaskName = taskName,
			CompletedAt = completedAt,
			DurationTicks = duration.Ticks,
			ResultMessage = string.IsNullOrWhiteSpace(resultMessage) ? null : resultMessage.Trim()
		});

		await db.SaveChangesAsync(ct);
	}
}
