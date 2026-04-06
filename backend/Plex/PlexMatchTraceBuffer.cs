using System.Collections.Concurrent;
using System.Text.Json;
using TubeArr.Backend.Serialization;

namespace TubeArr.Backend.Plex;

public sealed class PlexMatchTraceBuffer
{
	const int MaxEntries = 50;
	readonly ConcurrentQueue<PlexMatchTraceEntry> _queue = new();

	static readonly JsonSerializerOptions MatchJson = CreateMatchJsonOptions();

	static JsonSerializerOptions CreateMatchJsonOptions()
	{
		var o = new JsonSerializerOptions();
		TubeArrJsonSerializer.ApplyApiDefaults(o);
		return o;
	}

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

	internal static string? TryExtractChosenGuid(object? first)
	{
		if (first is null)
			return null;
		try
		{
			var json = JsonSerializer.Serialize(first, first.GetType(), MatchJson);
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.ValueKind != JsonValueKind.Object)
				return null;
			if (doc.RootElement.TryGetProperty("guid", out var g) && g.ValueKind == JsonValueKind.String)
				return g.GetString();
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
