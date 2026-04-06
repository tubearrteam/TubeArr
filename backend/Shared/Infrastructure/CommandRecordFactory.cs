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

	public Dictionary<string, object?> SnapshotCommand(CommandRuntimeRecord command) =>
		command.ToApiDictionary();

	public CommandRuntimeRecord CreateCommandRecord(
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

		var command = new CommandRuntimeRecord
		{
			Name = name,
			Message = message,
			Body = new Dictionary<string, object?>(body),
			Priority = "normal",
			Status = status,
			Result = result,
			Queued = queuedAt.ToString("O"),
			Started = startedAt.ToString("O"),
			Ended = isTerminalStatus ? endedAt.ToString("O") : null,
			Duration = isTerminalStatus ? FormatCommandDuration(endedAt - startedAt) : null,
			Trigger = trigger,
			StateChangeTime = (isTerminalStatus ? endedAt : startedAt).ToString("O"),
			SendUpdatesToClient = false,
			UpdateScheduledTask = false,
			LastExecutionTime = (isTerminalStatus ? endedAt : startedAt).ToString("O")
		};

		lock (_state.CommandsGate)
		{
			command.Id = _state.NextCommandId++;
			_state.Commands.Add(command);
			if (_state.Commands.Count > 50)
				_state.Commands.RemoveRange(0, _state.Commands.Count - 50);
		}

		return command;
	}

	public Dictionary<string, object?> UpdateCommandRecord(
		CommandRuntimeRecord command,
		Action<CommandRuntimeRecord, Dictionary<string, object?>> update)
	{
		lock (_state.CommandsGate)
		{
			var body = new Dictionary<string, object?>(command.Body);
			update(command, body);
			command.Body = body;
			return SnapshotCommand(command);
		}
	}

	public bool TryGetCommandById(int commandId, out CommandRuntimeRecord command)
	{
		lock (_state.CommandsGate)
		{
			var existing = _state.Commands.FirstOrDefault(c => c.Id == commandId);
			if (existing is null)
			{
				command = null!;
				return false;
			}

			command = existing;
			return true;
		}
	}

	public bool AnyCommand(Func<CommandRuntimeRecord, bool> predicate)
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
		CommandRuntimeRecord command,
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
