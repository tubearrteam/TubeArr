using System.Text.Json;

namespace TubeArr.Backend;

/// <summary>
/// Central service for channel resolution and search via yt-dlp.
/// ResolveExactChannel: exact resolution by channel ID, @handle, or channel URL.
/// SearchChannels: free-text search (ytsearch).
/// EnrichChannelForCreate: metadata for POST /channels when only channel ID is supplied.
/// </summary>
public static class YtDlpChannelLookupService
{
	/// <summary>Resolve a single channel by exact input (channel ID, @handle, or channel URL). Returns 0 or 1 strong match.</summary>
	/// <param name="timeoutMs">Max time for yt-dlp (default 60s). Use longer for resolve (e.g. 300000) so slow networks can complete.</param>
	public static async Task<(List<YtDlpChannelResultMapper.ChannelResultMap>? Results, string? ResolutionMethod)> ResolveExactChannelAsync(
		string executablePath,
		string input,
		CancellationToken ct = default,
		int timeoutMs = 60_000,
		Microsoft.Extensions.Logging.ILogger? logger = null)
	{
		var classification = ChannelResolveHelper.ClassifyInput(input);
		if (classification.Kind == ChannelResolveHelper.ChannelInputKind.Empty ||
		    classification.Kind == ChannelResolveHelper.ChannelInputKind.SearchTerm ||
		    classification.Kind == ChannelResolveHelper.ChannelInputKind.Unknown)
			return (null, null);

		var verbose = logger?.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug) == true;

		// Handles and channel IDs/URLs: resolve via uploads tab + --playlist-items 1 only (one video JSON â†’ channel_id / uploader_id).
		// Avoid --dump-single-json on @handle root: yt-dlp walks the full uploads list and times out.
		var url = classification.CanonicalUrl ?? "";
		if (string.IsNullOrWhiteSpace(url) && classification.Kind == ChannelResolveHelper.ChannelInputKind.Handle && !string.IsNullOrWhiteSpace(classification.Handle))
			url = $"https://www.youtube.com/@{classification.Handle}/videos";
		if (string.IsNullOrWhiteSpace(url))
			return (null, null);
		var args = YtDlpCommandBuilder.BuildExactResolveArgs(url, verbose);

		var (stdout, _, exitCode) = await YtDlpProcessRunner.RunAsync(executablePath, args, ct, timeoutMs, logger);
		if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
			return (null, null);

		try
		{
			// One JSON line: first video (or playlist wrapper with entries[0]).
			var map = default(YtDlpChannelResultMapper.ChannelResultMap);
			var firstLine = stdout.Trim().Split('\n')[0].Trim();
			if (string.IsNullOrEmpty(firstLine)) return (null, null);

			using (var doc = JsonDocument.Parse(firstLine))
			{
				var root = doc.RootElement;
				map = YtDlpChannelResultMapper.MapFromEntry(root, SlugHelper.Slugify);
				if (string.IsNullOrWhiteSpace(map.YoutubeChannelId) && root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
				{
					var first = entries[0];
					if (first.ValueKind == JsonValueKind.Object)
						map = YtDlpChannelResultMapper.MapFromEntry(first, SlugHelper.Slugify);
				}
			}
			if (string.IsNullOrWhiteSpace(map.YoutubeChannelId))
				return (null, null);
			string resolutionMethod = classification.Kind switch
			{
				ChannelResolveHelper.ChannelInputKind.ChannelId => "direct-channel-id",
				ChannelResolveHelper.ChannelInputKind.Handle => "direct-handle",
				_ => "direct-channel-url"
			};
			return (new List<YtDlpChannelResultMapper.ChannelResultMap> { map }, resolutionMethod);
		}
		catch
		{
			return (null, null);
		}
	}

	/// <summary>Search channels by free-text term. Returns deduplicated channel candidates.</summary>
	public static async Task<List<YtDlpChannelResultMapper.ChannelResultMap>> SearchChannelsAsync(
		string executablePath,
		string term,
		int maxResults = 20,
		CancellationToken ct = default)
	{
		var args = YtDlpCommandBuilder.BuildSearchArgs(term, maxResults);
		var (stdout, _, exitCode) = await YtDlpProcessRunner.RunAsync(executablePath, args, ct);
		if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
			return new List<YtDlpChannelResultMapper.ChannelResultMap>();

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<YtDlpChannelResultMapper.ChannelResultMap>();
		using var reader = new StringReader(stdout);
		string? line;
		while ((line = await reader.ReadLineAsync(ct)) != null)
		{
			line = line.Trim();
			if (string.IsNullOrEmpty(line)) continue;
			try
			{
				using var doc = JsonDocument.Parse(line);
				var map = YtDlpChannelResultMapper.MapFromEntry(doc.RootElement, SlugHelper.Slugify);
				if (string.IsNullOrWhiteSpace(map.YoutubeChannelId) || string.IsNullOrWhiteSpace(map.Title))
					continue;
				if (seen.Contains(map.YoutubeChannelId))
					continue;
				seen.Add(map.YoutubeChannelId);
				list.Add(map);
			}
			catch
			{
				// Skip malformed lines
			}
		}
		return list;
	}

	/// <summary>Enrich channel metadata for create (exact resolution by channel ID only).</summary>
	public static async Task<(string? Title, string? Description, string? ThumbnailUrl, string? ChannelUrl, string? Handle)?> EnrichChannelForCreateAsync(
		string executablePath,
		string youtubeChannelId,
		CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return null;
		var url = ChannelResolveHelper.GetCanonicalChannelVideosUrl(youtubeChannelId);
		var args = YtDlpCommandBuilder.BuildExactResolveArgs(url);
		var (stdout, _, exitCode) = await YtDlpProcessRunner.RunAsync(executablePath, args, ct);
		if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
			return null;
		try
		{
			var firstLine = stdout.Trim().Split('\n')[0].Trim();
			if (string.IsNullOrEmpty(firstLine)) return null;
			using var doc = JsonDocument.Parse(firstLine);
			var root = doc.RootElement;
			var map = YtDlpChannelResultMapper.MapFromEntry(root, SlugHelper.Slugify);
			if (string.IsNullOrWhiteSpace(map.YoutubeChannelId) && root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
			{
				var first = entries[0];
				if (first.ValueKind == JsonValueKind.Object)
					map = YtDlpChannelResultMapper.MapFromEntry(first, SlugHelper.Slugify);
			}
			return (map.Title, map.Description, map.ThumbnailUrl, map.ChannelUrl, map.Handle);
		}
		catch
		{
			return null;
		}
	}
}
