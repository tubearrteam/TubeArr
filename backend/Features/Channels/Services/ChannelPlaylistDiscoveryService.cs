using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Fetches channel playlists using the same preference order as video listing: YouTube Data API when enabled and prioritized,
/// then embedded YouTube page data, then yt-dlp on the channel Playlists tab URL.
/// </summary>
public sealed class ChannelPlaylistDiscoveryService
{
	readonly ChannelVideoDiscoveryService _channelVideoDiscoveryService;
	readonly YouTubeDataApiMetadataService _youTubeDataApiMetadataService;
	readonly ILogger<ChannelPlaylistDiscoveryService> _logger;

	public ChannelPlaylistDiscoveryService(
		ChannelVideoDiscoveryService channelVideoDiscoveryService,
		YouTubeDataApiMetadataService youTubeDataApiMetadataService,
		ILogger<ChannelPlaylistDiscoveryService> logger)
	{
		_channelVideoDiscoveryService = channelVideoDiscoveryService;
		_youTubeDataApiMetadataService = youTubeDataApiMetadataService;
		_logger = logger;
	}

	/// <summary>Fetches playlist metadata from YouTube Data API, channel HTML, or yt-dlp (no DB writes).</summary>
	public async Task<(string? ErrorMessage, IReadOnlyList<ChannelPlaylistDiscoveryItem>? Items)> DiscoverPlaylistsAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return (null, null);

		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
			return ("Channel has no valid YouTube channel ID; cannot load playlists.", null);

		if (progressReporter is not null)
		{
			await progressReporter.SetStageAsync(
				"playlistDiscovery",
				"Playlist discovery",
				0,
				0,
				detail: "Fetching playlists…",
				ct);
		}

		var youtubeChannelId = channel.YoutubeChannelId;
		IReadOnlyList<ChannelPlaylistDiscoveryItem> discovered = Array.Empty<ChannelPlaylistDiscoveryItem>();

		var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
		if (preference.UseYouTubeApi && preference.IsPrioritized(YouTubeApiMetadataPriorityItems.VideoListing))
		{
			discovered = await _youTubeDataApiMetadataService.TryDiscoverChannelPlaylistsAsync(db, youtubeChannelId, ct);
			if (discovered.Count > 0 && reportAcquisitionMethod is not null)
				await reportAcquisitionMethod(AcquisitionMethodIds.YouTubeDataApi);
		}

		if (discovered.Count == 0)
		{
			try
			{
				discovered = await _channelVideoDiscoveryService.DiscoverPlaylistsAsync(youtubeChannelId, ct);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Playlist discovery HTML parse failed for channel {ChannelId}", youtubeChannelId);
				discovered = Array.Empty<ChannelPlaylistDiscoveryItem>();
			}

			if (discovered.Count > 0 && reportAcquisitionMethod is not null)
				await reportAcquisitionMethod(AcquisitionMethodIds.Internal);
		}

		if (discovered.Count == 0)
		{
			discovered = await TryDiscoverPlaylistsViaYtDlpAsync(db, youtubeChannelId, ct);
			if (discovered.Count > 0 && reportAcquisitionMethod is not null)
				await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);
		}

		if (discovered.Count == 0)
		{
			return ("No playlists could be discovered for this channel. Enable the YouTube Data API (with Video listing prioritized), ensure the channel Playlists tab is reachable, or configure yt-dlp.", null);
		}

		var filtered = discovered
			.Where(p => !ChannelResolveHelper.IsChannelUploadsPlaylistId(youtubeChannelId, p.YoutubePlaylistId))
			.Where(p => !string.IsNullOrWhiteSpace(p.YoutubePlaylistId))
			.GroupBy(p => p.YoutubePlaylistId.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select(g => g.First())
			.ToList();

		if (filtered.Count == 0)
			return ("Only the uploads list was returned; no separate playlists to add.", null);

		return (null, filtered);
	}

	/// <summary>Persists discovered playlists for a channel (insert/update <see cref="PlaylistEntity"/> rows), then assigns <see cref="VideoEntity.PlaylistId"/> by fetching each playlist's items and matching to existing channel videos (uploads).</summary>
	public async Task<string?> UpsertDiscoveredPlaylistsAsync(
		TubeArrDbContext db,
		int channelId,
		IReadOnlyList<ChannelPlaylistDiscoveryItem> filtered,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return null;

		if (filtered.Count == 0)
			return null;

		var existing = await db.Playlists.Where(p => p.ChannelId == channel.Id).ToListAsync(ct);
		var byYoutubeId = existing.ToDictionary(p => p.YoutubePlaylistId, StringComparer.OrdinalIgnoreCase);

		foreach (var item in filtered)
		{
			var id = item.YoutubePlaylistId.Trim();
			if (byYoutubeId.TryGetValue(id, out var row))
			{
				if (!string.IsNullOrWhiteSpace(item.Title))
					row.Title = item.Title.Trim();
				row.Description = item.Description;
				row.ThumbnailUrl = item.ThumbnailUrl;
			}
			else
			{
				var entity = new PlaylistEntity
				{
					ChannelId = channel.Id,
					YoutubePlaylistId = id,
					Title = string.IsNullOrWhiteSpace(item.Title) ? id : item.Title.Trim(),
					Description = item.Description,
					ThumbnailUrl = item.ThumbnailUrl,
					Monitored = ShouldMonitorNewPlaylist(channel),
					Added = DateTimeOffset.UtcNow
				};
				db.Playlists.Add(entity);
				byYoutubeId[id] = entity;
			}
		}

		await db.SaveChangesAsync(ct);

		if (progressReporter is not null)
		{
			await progressReporter.SetStageAsync(
				"playlistDiscovery",
				"Playlist discovery",
				filtered.Count,
				filtered.Count,
				detail: $"Saved {filtered.Count} playlist(s).",
				ct);
		}

		await AssignPlaylistMembershipFromPlaylistItemsAsync(db, channelId, progressReporter, ct, reportAcquisitionMethod);

		return null;
	}

	public async Task<string?> FetchAndUpsertPlaylistsAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var (err, items) = await DiscoverPlaylistsAsync(db, channelId, progressReporter, ct, reportAcquisitionMethod);
		if (err == null && items is null)
			return null;

		if (!string.IsNullOrWhiteSpace(err))
			return err;

		return await UpsertDiscoveredPlaylistsAsync(db, channelId, items!, progressReporter, ct, reportAcquisitionMethod);
	}

	/// <summary>
	/// For each stored playlist, loads playlist item video ids (Data API, else yt-dlp), clears prior curated <see cref="VideoEntity.PlaylistId"/> for this channel, then assigns videos that still belong to the uploads library (first playlist row order wins).
	/// </summary>
	public async Task AssignPlaylistMembershipFromPlaylistItemsAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null || !ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
			return;

		var playlists = await ChannelDtoMapper.LoadPlaylistsOrderedByLatestUploadAsync(db, channelId, ct);
		if (playlists.Count == 0)
			return;

		if (progressReporter is not null)
		{
			await progressReporter.SetStageAsync(
				"playlistDiscovery",
				"Playlist discovery",
				0,
				playlists.Count,
				detail: "Matching videos to playlists…",
				ct);
		}

		var ownedPlaylistIds = playlists.Select(p => p.Id).ToHashSet();
		var videos = await db.Videos.Where(v => v.ChannelId == channelId).ToListAsync(ct);
		foreach (var v in videos)
		{
			if (v.PlaylistId.HasValue && ownedPlaylistIds.Contains(v.PlaylistId.Value))
				v.PlaylistId = null;
		}

		var unassigned = videos
			.Where(v => v.PlaylistId == null)
			.GroupBy(v => v.YoutubeVideoId, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		var ytConfig = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var preferApiFirst = ytConfig is { UseYouTubeApi: true } && !string.IsNullOrWhiteSpace(ytConfig.ApiKey);

		var done = 0;
		foreach (var pl in playlists)
		{
			ct.ThrowIfCancellationRequested();

			HashSet<string> idSet;
			if (preferApiFirst)
			{
				idSet = await _youTubeDataApiMetadataService.TryGetPlaylistItemVideoIdsAsync(db, pl.YoutubePlaylistId, ct);
				if (idSet.Count > 0)
				{
					if (reportAcquisitionMethod is not null)
						await reportAcquisitionMethod(AcquisitionMethodIds.YouTubeDataApi);
				}
				else
				{
					idSet = await TryGetPlaylistItemVideoIdsViaYtDlpAsync(db, pl.YoutubePlaylistId, ct);
					if (idSet.Count > 0 && reportAcquisitionMethod is not null)
						await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);
				}
			}
			else
			{
				idSet = await TryGetPlaylistItemVideoIdsViaYtDlpAsync(db, pl.YoutubePlaylistId, ct);
				if (idSet.Count > 0 && reportAcquisitionMethod is not null)
					await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);
			}

			foreach (var yid in idSet)
			{
				if (unassigned.TryGetValue(yid, out var ve))
				{
					ve.PlaylistId = pl.Id;
					unassigned.Remove(yid);
				}
			}

			done++;
			if (progressReporter is not null)
			{
				await progressReporter.SetStageAsync(
					"playlistDiscovery",
					"Playlist discovery",
					done,
					playlists.Count,
					detail: $"Matched playlist “{pl.Title}” ({done}/{playlists.Count})…",
					ct);
			}
		}

		await db.SaveChangesAsync(ct);
	}

	async Task<HashSet<string>> TryGetPlaylistItemVideoIdsViaYtDlpAsync(
		TubeArrDbContext db,
		string youtubePlaylistId,
		CancellationToken ct)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return result;

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);
		var url = $"https://www.youtube.com/playlist?list={Uri.EscapeDataString(youtubePlaylistId.Trim())}";
		List<JsonDocument> docs;
		try
		{
			docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
				executablePath,
				url,
				ct,
				playlistItems: null,
				timeoutMs: 300_000,
				flatPlaylist: true,
				playlistEnd: null,
				cookiesPath: cookiesPath);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "yt-dlp playlist items listing failed for playlistId={PlaylistId}", youtubePlaylistId);
			return result;
		}

		try
		{
			var items = ChannelMetadataAcquisitionService.FlattenYtDlpDiscoveryDocuments(docs);
			foreach (var i in items)
			{
				if (!string.IsNullOrWhiteSpace(i.YoutubeVideoId))
					result.Add(i.YoutubeVideoId.Trim());
			}
		}
		finally
		{
			foreach (var d in docs)
				d.Dispose();
		}

		return result;
	}

	static bool ShouldMonitorNewPlaylist(ChannelEntity channel)
	{
		if (!channel.Monitored)
			return false;

		if (channel.MonitorNewItems.HasValue && channel.MonitorNewItems.Value == 0)
			return false;

		return true;
	}

	async Task<IReadOnlyList<ChannelPlaylistDiscoveryItem>> TryDiscoverPlaylistsViaYtDlpAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return Array.Empty<ChannelPlaylistDiscoveryItem>();

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);
		var url = ChannelResolveHelper.GetCanonicalChannelPlaylistsUrl(youtubeChannelId);

		List<JsonDocument> docs;
		try
		{
			docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
				executablePath,
				url,
				ct,
				playlistItems: null,
				timeoutMs: 300_000,
				flatPlaylist: true,
				playlistEnd: null,
				cookiesPath: cookiesPath);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "yt-dlp playlist listing failed for channel {ChannelId}", youtubeChannelId);
			return Array.Empty<ChannelPlaylistDiscoveryItem>();
		}

		try
		{
			var items = new List<ChannelPlaylistDiscoveryItem>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var doc in docs)
				CollectYtDlpPlaylistEntries(doc.RootElement, items, seen);

			return items;
		}
		finally
		{
			foreach (var d in docs)
				d.Dispose();
		}
	}

	static void CollectYtDlpPlaylistEntries(JsonElement element, List<ChannelPlaylistDiscoveryItem> items, HashSet<string> seen)
	{
		if (TryCreateYtDlpPlaylistItem(element, out var item) && seen.Add(item.YoutubePlaylistId))
			items.Add(item);

		if (element.ValueKind != JsonValueKind.Object)
			return;

		if (element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
		{
			foreach (var entry in entries.EnumerateArray())
				CollectYtDlpPlaylistEntries(entry, items, seen);
		}
	}

	static bool TryCreateYtDlpPlaylistItem(JsonElement element, out ChannelPlaylistDiscoveryItem item)
	{
		item = default!;
		var id = GetYtDlpString(element, "id");
		if (string.IsNullOrWhiteSpace(id) || !LooksLikeYouTubePlaylistId(id))
			return false;

		var title = GetYtDlpString(element, "title") ?? GetYtDlpString(element, "fulltitle");
		var thumb = GetYtDlpString(element, "thumbnail") ?? GetYtDlpThumbnail(element);
		var desc = GetYtDlpString(element, "description");

		item = new ChannelPlaylistDiscoveryItem(id.Trim(), title, thumb, desc);
		return true;
	}

	static bool LooksLikeYouTubePlaylistId(string id)
	{
		if (string.IsNullOrWhiteSpace(id) || id.Length < 13)
			return false;

		if (id.StartsWith("UU", StringComparison.Ordinal))
			return false;

		return id.StartsWith("PL", StringComparison.Ordinal) ||
		       id.StartsWith("OL", StringComparison.Ordinal) ||
		       id.StartsWith("FL", StringComparison.Ordinal);
	}

	static string? GetYtDlpString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
			return null;

		var value = property.GetString()?.Trim();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	static string? GetYtDlpThumbnail(JsonElement element)
	{
		if (!element.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
			return null;

		string? last = null;
		foreach (var thumb in thumbnails.EnumerateArray())
		{
			if (thumb.ValueKind == JsonValueKind.Object &&
			    thumb.TryGetProperty("url", out var urlEl) &&
			    urlEl.ValueKind == JsonValueKind.String)
			{
				var u = urlEl.GetString()?.Trim();
				if (!string.IsNullOrWhiteSpace(u))
					last = u;
			}
		}

		return last;
	}
}
