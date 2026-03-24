using System.Collections.Generic;

namespace TubeArr.Backend;

public sealed class InMemoryCommandState
{
	public object CommandsGate { get; } = new object();
	public List<Dictionary<string, object?>> Commands { get; } = new();
	public int NextCommandId { get; set; } = 1;

	public Dictionary<string, object?>[] GetCommandsSnapshot()
	{
		lock (CommandsGate)
		{
			return Commands.ToArray();
		}
	}
}

