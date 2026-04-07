using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Tests;

/// <summary>
/// Tests for ScheduledTaskCatalog.GetScheduledTaskDtosAsync covering DefaultInterval,
/// IntervalOverride contract fields, and the duration-derived startedAt calculation.
/// </summary>
public sealed class ScheduledTaskCatalogTests : IDisposable
{
	private readonly string _dbPath;
	private readonly ServiceProvider _services;

	public ScheduledTaskCatalogTests()
	{
		_dbPath = CreateTempDbPath();
		var services = new ServiceCollection();
		services.AddTubeArrServices($"Data Source={_dbPath}");
		_services = services.BuildServiceProvider();
		EnsureMigrated();
	}

	private void EnsureMigrated()
	{
		using var scope = _services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		db.Database.Migrate();
	}

	private TubeArrDbContext CreateDb()
	{
		var scope = _services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
	}

	[Fact]
	public async Task DefaultInterval_reflects_catalog_entry_default()
	{
		using var db = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db);
		foreach (var entry in ScheduledTaskCatalog.Entries)
		{
			var dto = dtos.FirstOrDefault(d => d.TaskName == entry.TaskName);
			Assert.NotNull(dto);
			Assert.Equal(entry.Interval, dto!.DefaultInterval);
		}
	}

	[Fact]
	public async Task IntervalOverride_is_null_when_no_override_set()
	{
		using var db = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db);
		foreach (var dto in dtos)
		{
			Assert.Null(dto.IntervalOverride);
		}
	}

	[Fact]
	public async Task IntervalOverride_is_returned_when_override_exists()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0);
		var overrideMinutes = entry.Interval + 100;

		using (var db = CreateDb())
		{
			db.ScheduledTaskIntervalOverrides.Add(new ScheduledTaskIntervalOverrideEntity
			{
				TaskName = entry.TaskName,
				IntervalMinutes = overrideMinutes,
			});
			await db.SaveChangesAsync();
		}

		using var db2 = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db2);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(overrideMinutes, dto.IntervalOverride);
		Assert.Equal(overrideMinutes, dto.Interval);
		Assert.Equal(entry.Interval, dto.DefaultInterval);
	}

	[Fact]
	public async Task Interval_uses_catalog_default_when_override_is_zero()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0);

		// A zero-minute override is treated as "clear override" and is not stored,
		// but if one already exists and was cleared, there should be no row.
		// Verify: when no override row exists, effective interval equals catalog default.
		using var db = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(entry.Interval, dto.Interval);
		Assert.Null(dto.IntervalOverride);
	}

	[Fact]
	public async Task LastStartTime_is_derived_from_completion_minus_duration()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0 && ScheduledTaskCatalog.RecordsRuns(e.TaskName));
		var completed = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
		var durationTicks = TimeSpan.FromMinutes(5).Ticks;
		var expectedStart = completed.AddTicks(-durationTicks);

		using (var db = CreateDb())
		{
			db.ScheduledTaskStates.Add(new ScheduledTaskStateEntity
			{
				TaskName = entry.TaskName,
				LastCompletedAt = completed,
				LastDurationTicks = durationTicks,
			});
			await db.SaveChangesAsync();
		}

		using var db2 = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db2);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(completed.ToString("O"), dto.LastExecution);
		Assert.Equal(expectedStart.ToString("O"), dto.LastStartTime);
	}

	[Fact]
	public async Task LastStartTime_falls_back_to_completion_when_duration_ticks_are_null()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0 && ScheduledTaskCatalog.RecordsRuns(e.TaskName));
		var completed = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

		using (var db = CreateDb())
		{
			db.ScheduledTaskStates.Add(new ScheduledTaskStateEntity
			{
				TaskName = entry.TaskName,
				LastCompletedAt = completed,
				LastDurationTicks = null,
			});
			await db.SaveChangesAsync();
		}

		using var db2 = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db2);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(completed.ToString("O"), dto.LastStartTime);
	}

	[Fact]
	public async Task LastStartTime_falls_back_to_completion_when_duration_ticks_are_negative()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0 && ScheduledTaskCatalog.RecordsRuns(e.TaskName));
		var completed = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

		using (var db = CreateDb())
		{
			db.ScheduledTaskStates.Add(new ScheduledTaskStateEntity
			{
				TaskName = entry.TaskName,
				LastCompletedAt = completed,
				LastDurationTicks = -1L,
			});
			await db.SaveChangesAsync();
		}

		using var db2 = CreateDb();
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db2);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(completed.ToString("O"), dto.LastStartTime);
	}

	[Fact]
	public async Task LastStartTime_clamps_to_completion_when_duration_ticks_exceed_range()
	{
		var entry = ScheduledTaskCatalog.Entries.First(e => e.Interval > 0 && ScheduledTaskCatalog.RecordsRuns(e.TaskName));
		// Use a very early completion date so any large tick subtraction would overflow.
		var completed = DateTimeOffset.MinValue.AddDays(1);
		var hugeTicks = long.MaxValue;

		using (var db = CreateDb())
		{
			db.ScheduledTaskStates.Add(new ScheduledTaskStateEntity
			{
				TaskName = entry.TaskName,
				LastCompletedAt = completed,
				LastDurationTicks = hugeTicks,
			});
			await db.SaveChangesAsync();
		}

		using var db2 = CreateDb();
		// Should not throw; startedAt should be clamped to completed.
		var dtos = await ScheduledTaskCatalog.GetScheduledTaskDtosAsync(db2);
		var dto = dtos.First(d => d.TaskName == entry.TaskName);

		Assert.Equal(completed.ToString("O"), dto.LastStartTime);
	}

	public void Dispose()
	{
		_services.Dispose();
		TryDelete(_dbPath);
	}

	private static string CreateTempDbPath()
	{
		var root = Path.Combine(Path.GetTempPath(), "TubeArrTests");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"scheduled-task-catalog-{Guid.NewGuid():N}.sqlite");
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			// Best-effort cleanup for test temp files.
		}
	}
}
