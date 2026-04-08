using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace TubeArr.Backend.Plex;

public sealed class PlexMatchTraceBuffer
{
	const int MaxEntries = 50;
	readonly ConcurrentQueue<PlexMatchTraceEntry> _queue = new();

	public void Record(int type, string title, string guid, string pathSnippet, int resultCount, string? chosenGuid)
	{
		var e = new PlexMatchTraceEntry(
			DateTimeOffset.UtcNow,
			type,
			title ?? "",
			guid ?? "",
			pathSnippet.Length > 200 ? pathSnippet[..200] : pathSnippet,
			resultCount,
			chosenGuid);
		_queue.Enqueue(e);
		while (_queue.Count > MaxEntries && _queue.TryDequeue(out _))
		{
		}
	}

	public IReadOnlyList<PlexMatchTraceEntry> Snapshot() => _queue.ToArray();

	/// <summary>Best-effort read of the first match item's guid (Plex payloads are dictionaries or plain objects).</summary>
	internal static string? TryExtractChosenGuid(object? first)
	{
		if (first is null)
			return null;
		try
		{
			switch (first)
			{
				case IReadOnlyDictionary<string, object?> ro:
					return ro.TryGetValue("guid", out var v) ? v as string : null;
				case IDictionary<string, object?> dObj:
					return dObj.TryGetValue("guid", out var v2) ? v2 as string : null;
				case IDictionary dict:
					foreach (DictionaryEntry e in dict)
					{
						if (e.Key is string k && k.Equals("guid", StringComparison.OrdinalIgnoreCase) && e.Value is string s)
							return s;
					}
					return null;
				case JsonElement je when je.ValueKind == JsonValueKind.Object && je.TryGetProperty("guid", out var g) && g.ValueKind == JsonValueKind.String:
					return g.GetString();
			}

			foreach (var p in first.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
			{
				if (!p.Name.Equals("guid", StringComparison.OrdinalIgnoreCase) || p.GetIndexParameters().Length != 0)
					continue;
				if (p.GetValue(first) is string ps)
					return ps;
			}
		}
		catch
		{
			// ignore
		}

		return null;
	}
}

public sealed record PlexMatchTraceEntry(
	DateTimeOffset Utc,
	int Type,
	string Title,
	string Guid,
	string PathSnippet,
	int ResultCount,
	string? ChosenMatchGuid);
