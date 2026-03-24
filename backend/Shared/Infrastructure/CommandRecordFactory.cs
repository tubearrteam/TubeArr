using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public sealed class CommandRecordFactory
{
	readonly InMemoryCommandState _state;

	public CommandRecordFactory(InMemoryCommandState state)
	{
		_state = state;
	}

	public static string FormatCommandDuration(TimeSpan duration)
	{
		return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
	}

	public Dictionary<string, object?> SnapshotCommand(Dictionary<string, object?> command)
	{
		var snapshot = new Dictionary<string, object?>(command);
		if (snapshot.TryGetValue("body", out var bodyObj) && bodyObj is Dictionary<string, object?> body)
			snapshot["body"] = new Dictionary<string, object?>(body);
		return snapshot;
	}

	public Dictionary<string, object?> CreateCommandRecord(
		string name,
		string trigger,
		Dictionary<string, object?> body,
		string status,
		string result,
		string message,
		DateTimeOffset queuedAt,
		DateTimeOffset startedAt,
		DateTimeOffset endedAt)
	{
		var isTerminalStatus =
			string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(status, "aborted", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(status, "orphaned", StringComparison.OrdinalIgnoreCase);

		var command = new Dictionary<string, object?>
		{
			["name"] = name,
			["commandName"] = name,
			["message"] = message,
			["body"] = new Dictionary<string, object?>(body),
			["priority"] = "normal",
			["status"] = status,
			["result"] = result,
			["queued"] = queuedAt.ToString("O"),
			["started"] = startedAt.ToString("O"),
			["ended"] = isTerminalStatus ? endedAt.ToString("O") : null,
			["duration"] = isTerminalStatus ? FormatCommandDuration(endedAt - startedAt) : null,
			["trigger"] = trigger,
			["stateChangeTime"] = (isTerminalStatus ? endedAt : startedAt).ToString("O"),
			["sendUpdatesToClient"] = false,
			["updateScheduledTask"] = false,
			["lastExecutionTime"] = (isTerminalStatus ? endedAt : startedAt).ToString("O")
		};

		lock (_state.CommandsGate)
		{
			command["id"] = _state.NextCommandId++;
			_state.Commands.Add(command);
			if (_state.Commands.Count > 50)
				_state.Commands.RemoveRange(0, _state.Commands.Count - 50);
		}

		return command;
	}

	public Dictionary<string, object?> UpdateCommandRecord(
		Dictionary<string, object?> command,
		Action<Dictionary<string, object?>, Dictionary<string, object?>> update)
	{
		lock (_state.CommandsGate)
		{
			var body = command.TryGetValue("body", out var bodyObj) && bodyObj is Dictionary<string, object?> existingBody
				? new Dictionary<string, object?>(existingBody)
				: new Dictionary<string, object?>();

			update(command, body);
			command["body"] = body;
			return SnapshotCommand(command);
		}
	}

	public bool TryGetCommandById(int commandId, out Dictionary<string, object?> command)
	{
		lock (_state.CommandsGate)
		{
			var existing = _state.Commands.FirstOrDefault(c =>
				c.TryGetValue("id", out var idObj) &&
				idObj is int existingId &&
				existingId == commandId);

			if (existing is null)
			{
				command = null!;
				return false;
			}

			command = existing;
			return true;
		}
	}

	public bool AnyCommand(Func<Dictionary<string, object?>, bool> predicate)
	{
		lock (_state.CommandsGate)
		{
			foreach (var command in _state.Commands)
			{
				if (predicate(command))
					return true;
			}
		}

		return false;
	}

	public Task BroadcastCommandUpdateAsync(
		IRealtimeEventBroadcaster realtime,
		Dictionary<string, object?> command,
		CancellationToken ct = default)
	{
		return realtime.BroadcastAsync("command", new { action = "updated", resource = SnapshotCommand(command) }, ct);
	}

	public Dictionary<string, object?> ToMetadataProgressResource(MetadataProgressSnapshot snapshot)
	{
		return new Dictionary<string, object?>
		{
			["stages"] = snapshot.Stages.Select(stage => new Dictionary<string, object?>
			{
				["key"] = stage.Key,
				["label"] = stage.Label,
				["completed"] = stage.Completed,
				["total"] = stage.Total,
				["percent"] = stage.Percent,
				["detail"] = stage.Detail,
				["errors"] = stage.Errors.ToArray()
			}).ToArray(),
			["errors"] = snapshot.Errors.ToArray()
		};
	}
}

