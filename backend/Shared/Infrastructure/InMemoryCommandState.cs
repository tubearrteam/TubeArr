using System.Collections.Generic;

namespace TubeArr.Backend;

public sealed class InMemoryCommandState
{
	public object CommandsGate { get; } = new object();
	public List<CommandRuntimeRecord> Commands { get; } = new();
	public int NextCommandId { get; set; } = 1;

	public Dictionary<string, object?>[] GetCommandsSnapshot()
	{
		lock (CommandsGate)
		{
			var arr = new Dictionary<string, object?>[Commands.Count];
			for (var i = 0; i < Commands.Count; i++)
				arr[i] = Commands[i].ToApiDictionary();
			return arr;
		}
	}

	/// <summary>Used by scheduled tasks to avoid duplicate runs of the same task name.</summary>
	public bool IsCommandNameRunning(string taskName)
	{
		lock (CommandsGate)
		{
			foreach (var cmd in Commands)
			{
				if (!string.Equals(cmd.Name, taskName, StringComparison.OrdinalIgnoreCase))
					continue;
				if (string.Equals(cmd.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
				    string.Equals(cmd.Status, "started", StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}

		return false;
	}
}
