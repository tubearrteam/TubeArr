using System.Globalization;
using System.Text.Json;

namespace TubeArr.Backend;

public sealed partial class ChannelMetadataAcquisitionService
{
	static void CollectYtDlpDiscoveryItems(JsonElement element, List<ChannelVideoDiscoveryItem> items, HashSet<string> seen)
	{
		if (TryCreateYtDlpDiscoveryItem(element, out var item) && seen.Add(item.YoutubeVideoId))
			items.Add(item);

		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
			{
				foreach (var entry in entries.EnumerateArray())
					CollectYtDlpDiscoveryItems(entry, items, seen);
			}
		}
	}

	static bool TryCreateYtDlpDiscoveryItem(JsonElement element, out ChannelVideoDiscoveryItem item)
	{
		item = default!;
		var youtubeVideoId = GetYtDlpVideoId(element);
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var title = GetYtDlpString(element, "title") ?? GetYtDlpString(element, "fulltitle");
		var thumbnailUrl = GetYtDlpString(element, "thumbnail") ?? GetYtDlpThumbnail(element);
		var runtime = GetYtDlpInt(element, "duration");

		item = new ChannelVideoDiscoveryItem(
			YoutubeVideoId: youtubeVideoId,
			Title: title,
			ThumbnailUrl: thumbnailUrl,
			Runtime: runtime);
		return true;
	}

	static void CollectYtDlpVideoMetadata(JsonElement element, Dictionary<string, VideoWatchPageMetadata> items)
	{
		if (TryCreateYtDlpVideoMetadata(element, out var item))
			items[item.YoutubeVideoId] = item;

		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
			{
				foreach (var entry in entries.EnumerateArray())
					CollectYtDlpVideoMetadata(entry, items);
			}
		}
	}

	static bool TryCreateYtDlpVideoMetadata(JsonElement element, out VideoWatchPageMetadata item)
	{
		item = default!;
		var youtubeVideoId = GetYtDlpVideoId(element);
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var metadata = ParseYtDlpVideoMetadata(element, youtubeVideoId);
		if (string.IsNullOrWhiteSpace(metadata.Title) &&
			string.IsNullOrWhiteSpace(metadata.Description) &&
			string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			!metadata.UploadDateUtc.HasValue &&
			!metadata.Runtime.HasValue)
		{
			return false;
		}

		item = metadata;
		return true;
	}

	static VideoWatchPageMetadata ParseYtDlpVideoMetadata(JsonElement element, string youtubeVideoId)
	{
		var title = GetYtDlpString(element, "title") ?? GetYtDlpString(element, "fulltitle");
		var description = GetYtDlpString(element, "description");
		var thumbnailUrl = GetYtDlpString(element, "thumbnail") ?? GetYtDlpThumbnail(element);
		var uploadDateUtc = ParseYtDlpUploadDate(element);
		var airDateUtc = uploadDateUtc;
		var airDate = airDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var runtime = GetYtDlpInt(element, "duration");

		return new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			UploadDateUtc: uploadDateUtc,
			AirDateUtc: airDateUtc,
			AirDate: airDate,
			Overview: description,
			Runtime: runtime,
			IsShort: null,
			IsLivestream: ParseYtDlpIsLivestream(element));
	}

	static bool? ParseYtDlpIsLivestream(JsonElement element)
	{
		var liveStatus = GetYtDlpString(element, "live_status");
		if (!string.IsNullOrWhiteSpace(liveStatus))
		{
			switch (liveStatus.Trim().ToLowerInvariant())
			{
				case "is_live":
				case "was_live":
				case "is_upcoming":
				case "post_live":
					return true;
			}
		}

		var wasLive = GetYtDlpBool(element, "was_live");
		if (wasLive == true)
			return true;
		var isLive = GetYtDlpBool(element, "is_live");
		if (isLive == true)
			return true;
		var isUpcoming = GetYtDlpBool(element, "is_upcoming");
		if (isUpcoming == true)
			return true;

		return null;
	}

	static string? GetYtDlpVideoId(JsonElement element)
	{
		return GetYtDlpString(element, "id")
			?? GetYtDlpString(element, "video_id")
			?? GetYtDlpString(element, "display_id");
	}

	static string? GetYtDlpString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
			return null;

		var value = property.GetString()?.Trim();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	static int? GetYtDlpInt(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
			return null;

		return property.TryGetInt32(out var parsed) ? parsed : null;
	}

	static bool? GetYtDlpBool(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property))
			return null;
		return property.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null
		};
	}

	static string? GetYtDlpThumbnail(JsonElement element)
	{
		if (!element.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
			return null;

		string? value = null;
		foreach (var thumbnail in thumbnails.EnumerateArray())
		{
			var candidate = GetYtDlpString(thumbnail, "url");
			if (!string.IsNullOrWhiteSpace(candidate))
				value = candidate;
		}

		return value;
	}

	static DateTimeOffset? ParseYtDlpUploadDate(JsonElement element)
	{
		var uploadDate = GetYtDlpString(element, "upload_date") ?? GetYtDlpString(element, "release_date");
		if (!string.IsNullOrWhiteSpace(uploadDate) &&
			DateTimeOffset.TryParseExact(
				uploadDate,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
				out var parsed))
		{
			return parsed;
		}

		if (element.TryGetProperty("timestamp", out var timestampElement) &&
			timestampElement.ValueKind == JsonValueKind.Number &&
			timestampElement.TryGetInt64(out var timestamp))
		{
			return DateTimeOffset.FromUnixTimeSeconds(timestamp);
		}

		return null;
	}
}
