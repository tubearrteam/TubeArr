using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TubeArr.Backend;

/// <summary>
/// In-memory command row for API and realtime; persisted queue jobs reference <see cref="Id"/>.
/// </summary>
public sealed class CommandRuntimeRecord
{
	public int Id { get; set; }
	public string Name { get; set; } = "";
	public string Message { get; set; } = "";
	public string Priority { get; set; } = "normal";
	public string Status { get; set; } = "queued";
	public string Result { get; set; } = "unknown";
	public string? Queued { get; set; }
	public string? Started { get; set; }
	public string? Ended { get; set; }
	public string? Duration { get; set; }
	public string Trigger { get; set; } = "";
	public string? StateChangeTime { get; set; }
	public bool SendUpdatesToClient { get; set; }
	public bool UpdateScheduledTask { get; set; }
	public string? LastExecutionTime { get; set; }
	public Dictionary<string, object?> Body { get; set; } = new();

	/// <summary>Supports legacy update lambdas that mutated a dictionary (top-level fields only; body uses <see cref="Body"/>).</summary>
	public object? this[string key]
	{
		get => key switch
		{
			"id" => Id,
			"name" => Name,
			"commandName" => Name,
			"message" => Message,
			"priority" => Priority,
			"status" => Status,
			"result" => Result,
			"queued" => Queued,
			"started" => Started,
			"ended" => Ended,
			"duration" => Duration,
			"trigger" => Trigger,
			"stateChangeTime" => StateChangeTime,
			"sendUpdatesToClient" => SendUpdatesToClient,
			"updateScheduledTask" => UpdateScheduledTask,
			"lastExecutionTime" => LastExecutionTime,
			_ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
		};
		set
		{
			switch (key)
			{
				case "id":
					Id = value switch
					{
						int i => i,
						null => 0,
						_ => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
					};
					break;
				case "name":
				case "commandName":
					Name = value?.ToString() ?? "";
					break;
				case "message":
					Message = value?.ToString() ?? "";
					break;
				case "priority":
					Priority = value?.ToString() ?? "normal";
					break;
				case "status":
					Status = value?.ToString() ?? "";
					break;
				case "result":
					Result = value?.ToString() ?? "";
					break;
				case "queued":
					Queued = value?.ToString();
					break;
				case "started":
					Started = value?.ToString();
					break;
				case "ended":
					Ended = value?.ToString();
					break;
				case "duration":
					Duration = value?.ToString();
					break;
				case "trigger":
					Trigger = value?.ToString() ?? "";
					break;
				case "stateChangeTime":
					StateChangeTime = value?.ToString();
					break;
				case "sendUpdatesToClient":
					SendUpdatesToClient = value is bool b ? b : value is not null && Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
					break;
				case "updateScheduledTask":
					UpdateScheduledTask = value is bool b2 ? b2 : value is not null && Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
					break;
				case "lastExecutionTime":
					LastExecutionTime = value?.ToString();
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(key), key, null);
			}
		}
	}

	public Dictionary<string, object?> ToApiDictionary()
	{
		return new Dictionary<string, object?>
		{
			["id"] = Id,
			["name"] = Name,
			["commandName"] = Name,
			["message"] = Message,
			["body"] = new Dictionary<string, object?>(Body),
			["priority"] = Priority,
			["status"] = Status,
			["result"] = Result,
			["queued"] = Queued,
			["started"] = Started,
			["ended"] = Ended,
			["duration"] = Duration,
			["trigger"] = Trigger,
			["stateChangeTime"] = StateChangeTime,
			["sendUpdatesToClient"] = SendUpdatesToClient,
			["updateScheduledTask"] = UpdateScheduledTask,
			["lastExecutionTime"] = LastExecutionTime,
		};
	}

	public static bool TryGetCommandId(CommandRuntimeRecord? command, out int commandId)
	{
		if (command is { Id: > 0 } c)
		{
			commandId = c.Id;
			return true;
		}

		commandId = 0;
		return false;
	}

	public static bool TryGetCommandName(CommandRuntimeRecord command, out string name)
	{
		if (!string.IsNullOrWhiteSpace(command.Name))
		{
			name = command.Name;
			return true;
		}

		name = string.Empty;
		return false;
	}

	public static bool TryGetCommandStatus(CommandRuntimeRecord command, out string status)
	{
		if (!string.IsNullOrWhiteSpace(command.Status))
		{
			status = command.Status;
			return true;
		}

		status = string.Empty;
		return false;
	}

	public static bool CommandBodyTargetsChannel(CommandRuntimeRecord command, int channelId)
	{
		var body = command.Body;
		if (TryGetSingleChannelId(body, out var singleId) && singleId == channelId)
			return true;
		if (TryGetChannelIds(body, out var channelIds) && channelIds.Contains(channelId))
			return true;

		return false;
	}

	static bool TryGetSingleChannelId(Dictionary<string, object?> body, out int channelId)
	{
		channelId = 0;
		if (!body.TryGetValue("channelId", out var channelObj))
			return false;

		if (channelObj is int cid)
		{
			channelId = cid;
			return channelId > 0;
		}

		if (channelObj is JsonElement channelJson &&
		    channelJson.ValueKind == JsonValueKind.Number &&
		    channelJson.TryGetInt32(out var parsed))
		{
			channelId = parsed;
			return channelId > 0;
		}

		return false;
	}

	static bool TryGetChannelIds(Dictionary<string, object?> body, out HashSet<int> channelIds)
	{
		channelIds = new HashSet<int>();
		if (!body.TryGetValue("channelIds", out var channelIdsObj))
			return false;

		if (channelIdsObj is int[] intArray)
		{
			foreach (var id in intArray)
			{
				if (id > 0)
					channelIds.Add(id);
			}

			return channelIds.Count > 0;
		}

		if (channelIdsObj is JsonElement channelIdsJson && channelIdsJson.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in channelIdsJson.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var id) && id > 0)
					channelIds.Add(id);
			}

			return channelIds.Count > 0;
		}

		return false;
	}
}
