using System.Threading;

namespace TubeArr.Backend;

public sealed record MetadataProgressStageSnapshot(
	string Key,
	string Label,
	int Completed,
	int Total,
	double Percent,
	string? Detail,
	IReadOnlyList<string> Errors);

public sealed record MetadataProgressSnapshot(
	IReadOnlyList<MetadataProgressStageSnapshot> Stages,
	IReadOnlyList<string> Errors);

public sealed class MetadataProgressReporter
{
	readonly Func<MetadataProgressSnapshot, CancellationToken, Task>? _onChanged;
	readonly object _gate = new();
	readonly SemaphoreSlim _publishSequential = new(1, 1);
	readonly Dictionary<string, StageState> _stages = new(StringComparer.OrdinalIgnoreCase);
	readonly List<string> _errors = new();

	public MetadataProgressReporter(Func<MetadataProgressSnapshot, CancellationToken, Task>? onChanged = null)
	{
		_onChanged = onChanged;
	}

	public Task SetStageAsync(
		string key,
		string label,
		int completed,
		int total,
		string? detail = null,
		CancellationToken ct = default)
	{
		lock (_gate)
		{
			var stage = GetOrCreateStage(key, label);
			stage.Completed = Math.Max(0, completed);
			stage.Total = Math.Max(0, total);
			stage.Detail = detail;
		}

		return PublishAsync(ct);
	}

	public Task AddToStageTotalAsync(
		string key,
		string label,
		int additionalTotal,
		string? detail = null,
		CancellationToken ct = default)
	{
		lock (_gate)
		{
			var stage = GetOrCreateStage(key, label);
			stage.Total = Math.Max(0, stage.Total + additionalTotal);
			stage.Detail = detail;
		}

		return PublishAsync(ct);
	}

	public Task IncrementStageAsync(
		string key,
		string label,
		int total,
		string? detail = null,
		CancellationToken ct = default)
	{
		lock (_gate)
		{
			var stage = GetOrCreateStage(key, label);
			stage.Total = Math.Max(stage.Total, total);
			stage.Completed = Math.Min(stage.Total, stage.Completed + 1);
			stage.Detail = detail;
		}

		return PublishAsync(ct);
	}

	public Task AddStageErrorAsync(
		string key,
		string label,
		string error,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(error))
			return Task.CompletedTask;

		lock (_gate)
		{
			var stage = GetOrCreateStage(key, label);
			if (!stage.Errors.Contains(error, StringComparer.Ordinal))
				stage.Errors.Add(error);
			if (!_errors.Contains(error, StringComparer.Ordinal))
				_errors.Add(error);
		}

		return PublishAsync(ct);
	}

	public Task AddErrorAsync(string error, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(error))
			return Task.CompletedTask;

		lock (_gate)
		{
			if (!_errors.Contains(error, StringComparer.Ordinal))
				_errors.Add(error);
		}

		return PublishAsync(ct);
	}

	public MetadataProgressSnapshot GetSnapshot()
	{
		lock (_gate)
		{
			return CreateSnapshot();
		}
	}

	StageState GetOrCreateStage(string key, string label)
	{
		if (_stages.TryGetValue(key, out var existing))
		{
			existing.Label = label;
			return existing;
		}

		var created = new StageState(key, label);
		_stages[key] = created;
		return created;
	}

	MetadataProgressSnapshot CreateSnapshot()
	{
		static double GetPercent(int completed, int total)
		{
			if (total <= 0)
				return 0;

			return Math.Round((double)completed / total * 100, 1);
		}

		var stageOrder = new[] { "rssFeedSync", "channelVideoListFetching", "videoDetailFetching", "ffprobe", "mapUnmapped" };
		var orderedStages = _stages.Values
			.OrderBy(stage =>
			{
				var index = Array.FindIndex(stageOrder, key => string.Equals(key, stage.Key, StringComparison.OrdinalIgnoreCase));
				return index >= 0 ? index : int.MaxValue;
			})
			.ThenBy(stage => stage.Label, StringComparer.OrdinalIgnoreCase)
			.Select(stage => new MetadataProgressStageSnapshot(
				stage.Key,
				stage.Label,
				stage.Completed,
				stage.Total,
				GetPercent(stage.Completed, stage.Total),
				stage.Detail,
				stage.Errors.ToArray()))
			.ToArray();

		return new MetadataProgressSnapshot(orderedStages, _errors.ToArray());
	}

	async Task PublishAsync(CancellationToken ct)
	{
		if (_onChanged is null)
			return;

		await _publishSequential.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			MetadataProgressSnapshot snapshot;
			lock (_gate)
			{
				snapshot = CreateSnapshot();
			}

			await _onChanged(snapshot, ct).ConfigureAwait(false);
		}
		finally
		{
			_publishSequential.Release();
		}
	}

	sealed class StageState
	{
		public StageState(string key, string label)
		{
			Key = key;
			Label = label;
		}

		public string Key { get; }
		public string Label { get; set; }
		public int Completed { get; set; }
		public int Total { get; set; }
		public string? Detail { get; set; }
		public List<string> Errors { get; } = new();
	}
}
