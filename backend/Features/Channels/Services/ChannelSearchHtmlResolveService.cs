using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

public sealed class ChannelSearchHtmlResolveService
{
	public sealed record ChannelSearchHtmlCandidate(
		string YoutubeChannelId,
		string Title,
		string? Description = null,
		string? ThumbnailUrl = null);

	// Fast precheck: find at least one channelRenderer.channelId.
	static readonly Regex QuickChannelIdRegex = new(
		@"""channelRenderer""\s*:\s*\{\s*""channelId""\s*:\s*""(UC[0-9A-Za-z_-]{22})""",
		RegexOptions.Compiled);

	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<ChannelSearchHtmlResolveService> _logger;

	public ChannelSearchHtmlResolveService(IHttpClientFactory httpClientFactory, ILogger<ChannelSearchHtmlResolveService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<string?> ResolveFirstChannelIdAsync(string term, CancellationToken ct = default)
	{
		var results = await SearchChannelsAsync(term, maxResults: 1, ct);
		return results.Count == 0 ? null : results[0].YoutubeChannelId;
	}

	public async Task<List<ChannelSearchHtmlCandidate>> SearchChannelsAsync(
		string term,
		int maxResults,
		CancellationToken ct = default)
	{
		var trimmed = (term ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(trimmed) || maxResults <= 0)
			return new List<ChannelSearchHtmlCandidate>();

		var client = _httpClientFactory.CreateClient("YouTubePage");
		var url = "results?search_query=" + Uri.EscapeDataString(trimmed);
		using var response = await client.GetAsync(url, ct);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogDebug("Search page request failed status={StatusCode} term={Term}", (int)response.StatusCode, trimmed);
			return new List<ChannelSearchHtmlCandidate>();
		}

		var html = await response.Content.ReadAsStringAsync(ct);
		return ExtractChannelCandidatesFromSearchResultsHtml(html, maxResults);
	}

	public static string? ExtractFirstChannelIdFromSearchResultsHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		var quick = QuickChannelIdFromHtml(html);
		if (ChannelResolveHelper.IsValidChannelId(quick))
			return quick;

		using var doc = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialData = ",
			"window[\"ytInitialData\"] = ",
			"ytInitialData = ");
		if (doc is null)
			return null;

		var candidates = ExtractChannelCandidatesFromYtInitialData(doc.RootElement, maxResults: 1);
		return candidates.Count > 0 ? candidates[0].YoutubeChannelId : null;
	}

	public static List<ChannelSearchHtmlCandidate> ExtractChannelCandidatesFromSearchResultsHtml(string html, int maxResults)
	{
		if (string.IsNullOrWhiteSpace(html) || maxResults <= 0)
			return new List<ChannelSearchHtmlCandidate>();

		// Fast precheck: if structure changes completely, still return something quickly.
		var quickIds = ExtractQuickIds(html, maxResults);
		if (quickIds.Count > 0)
		{
			// If ytInitialData is extractable, prefer accurate titles/thumbnails below.
			using var doc = YouTubePageJsonHelper.TryExtractJsonDocument(
				html,
				"var ytInitialData = ",
				"window[\"ytInitialData\"] = ",
				"ytInitialData = ");
			if (doc is not null)
			{
				var fromJson = ExtractChannelCandidatesFromYtInitialData(doc.RootElement, maxResults);
				if (fromJson.Count > 0)
					return fromJson;
			}

			return quickIds.Select(id => new ChannelSearchHtmlCandidate(id, id, null, null)).ToList();
		}

		// Full path.
		using var fullDoc = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialData = ",
			"window[\"ytInitialData\"] = ",
			"ytInitialData = ");
		if (fullDoc is null)
			return new List<ChannelSearchHtmlCandidate>();

		var candidates = ExtractChannelCandidatesFromYtInitialData(fullDoc.RootElement, maxResults);
		return candidates;
	}

	static string? QuickChannelIdFromHtml(string html)
	{
		var m = QuickChannelIdRegex.Match(html);
		return m.Success ? m.Groups[1].Value : null;
	}

	static List<string> ExtractQuickIds(string html, int maxResults)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<string>();
		foreach (Match m in QuickChannelIdRegex.Matches(html))
		{
			if (!m.Success) continue;
			var id = m.Groups[1].Value;
			if (!ChannelResolveHelper.IsValidChannelId(id)) continue;
			if (!ids.Add(id)) continue;
			list.Add(id);
			if (list.Count >= maxResults)
				break;
		}
		return list;
	}

	static List<ChannelSearchHtmlCandidate> ExtractChannelCandidatesFromYtInitialData(JsonElement root, int maxResults)
	{
		var list = new List<ChannelSearchHtmlCandidate>();

		if (TryCollectCandidatesFromTwoColumnResults(root, maxResults, list))
			return list;

		// Continuations (appendContinuationItemsAction) can contain additional channelRenderer blocks.
		if (root.ValueKind == JsonValueKind.Object &&
		    root.TryGetProperty("onResponseReceivedCommands", out var commands) &&
		    commands.ValueKind == JsonValueKind.Array)
		{
			foreach (var cmd in commands.EnumerateArray())
			{
				if (list.Count >= maxResults) break;
				if (cmd.ValueKind != JsonValueKind.Object) continue;
				if (!cmd.TryGetProperty("appendContinuationItemsAction", out var append) || append.ValueKind != JsonValueKind.Object)
					continue;
				if (!append.TryGetProperty("continuationItems", out var items) || items.ValueKind != JsonValueKind.Array)
					continue;

				CollectCandidatesFromItemList(items, maxResults, list);
			}
		}

		return list;
	}

	static bool TryCollectCandidatesFromTwoColumnResults(JsonElement root, int maxResults, List<ChannelSearchHtmlCandidate> list)
	{
		if (root.ValueKind != JsonValueKind.Object)
			return false;

		if (!root.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Object)
			return false;
		if (!contents.TryGetProperty("twoColumnSearchResultsRenderer", out var twoCol) || twoCol.ValueKind != JsonValueKind.Object)
			return false;
		if (!twoCol.TryGetProperty("primaryContents", out var primary) || primary.ValueKind != JsonValueKind.Object)
			return false;
		if (!primary.TryGetProperty("sectionListRenderer", out var sectionList) || sectionList.ValueKind != JsonValueKind.Object)
			return false;
		if (!sectionList.TryGetProperty("contents", out var sections) || sections.ValueKind != JsonValueKind.Array)
			return false;

		foreach (var section in sections.EnumerateArray())
		{
			if (list.Count >= maxResults) break;
			if (section.ValueKind != JsonValueKind.Object) continue;
			if (!section.TryGetProperty("itemSectionRenderer", out var itemSection) || itemSection.ValueKind != JsonValueKind.Object)
				continue;
			if (!itemSection.TryGetProperty("contents", out var items) || items.ValueKind != JsonValueKind.Array)
				continue;

			CollectCandidatesFromItemList(items, maxResults, list);
		}

		return list.Count > 0;
	}

	static void CollectCandidatesFromItemList(JsonElement itemsArray, int maxResults, List<ChannelSearchHtmlCandidate> list)
	{
		if (itemsArray.ValueKind != JsonValueKind.Array)
			return;

		foreach (var item in itemsArray.EnumerateArray())
		{
			if (list.Count >= maxResults) break;
			if (item.ValueKind != JsonValueKind.Object) continue;

			if (TryExtractCandidateFromContainer(item, out var candidate))
				list.Add(candidate);
			else if (item.TryGetProperty("richItemRenderer", out var rich) && rich.ValueKind == JsonValueKind.Object &&
			         rich.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
			{
				if (TryExtractCandidateFromContainer(content, out candidate))
					list.Add(candidate);
			}
		}
	}

	static bool TryExtractCandidateFromContainer(JsonElement container, out ChannelSearchHtmlCandidate candidate)
	{
		candidate = default!;
		if (container.ValueKind != JsonValueKind.Object)
			return false;

		if (!container.TryGetProperty("channelRenderer", out var channelRenderer) || channelRenderer.ValueKind != JsonValueKind.Object)
			return false;

		if (!channelRenderer.TryGetProperty("channelId", out var idElement) || idElement.ValueKind != JsonValueKind.String)
			return false;

		var id = idElement.GetString()?.Trim();
		if (!ChannelResolveHelper.IsValidChannelId(id ?? string.Empty))
			return false;

		var title = ReadTextValue(channelRenderer, "title") ?? id!;
		var description = ReadTextValue(channelRenderer, "descriptionSnippet") ?? ReadTextValue(channelRenderer, "description");
		var thumbnailUrl = TryReadThumbnailUrl(channelRenderer);

		candidate = new ChannelSearchHtmlCandidate(id!, title, description, thumbnailUrl);
		return true;
	}

	static string? ReadTextValue(JsonElement parent, string propertyName)
	{
		if (!parent.TryGetProperty(propertyName, out var element))
			return null;
		return ReadTextValue(element);
	}

	static string? ReadTextValue(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.String)
		{
			var s = element.GetString()?.Trim();
			return string.IsNullOrWhiteSpace(s) ? null : WebUtility.HtmlDecode(s);
		}

		if (element.ValueKind != JsonValueKind.Object)
			return null;

		if (element.TryGetProperty("simpleText", out var simpleText) && simpleText.ValueKind == JsonValueKind.String)
		{
			var s = simpleText.GetString()?.Trim();
			return string.IsNullOrWhiteSpace(s) ? null : WebUtility.HtmlDecode(s);
		}

		if (element.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
		{
			var parts = new List<string>();
			foreach (var run in runs.EnumerateArray())
			{
				if (run.ValueKind != JsonValueKind.Object) continue;
				if (run.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
				{
					var text = textEl.GetString()?.Trim();
					if (!string.IsNullOrWhiteSpace(text))
						parts.Add(text);
				}
			}

			var s = string.Concat(parts);
			if (string.IsNullOrWhiteSpace(s))
				return null;
			return WebUtility.HtmlDecode(s);
		}

		return null;
	}

	static string? TryReadThumbnailUrl(JsonElement channelRenderer)
	{
		// Common shapes:
		// - channelRenderer.thumbnail.thumbnails[]
		// - channelRenderer.avatar.thumbnails[]
		// - channelRenderer.channelThumbnailSupportedRenderers.{channelThumbnailWithLinkRenderer.thumbnail.thumbnails[]}
		if (TryGetThumbnails(channelRenderer, "thumbnail", out var thumbs))
			return ReadLastNonEmptyThumbnailUrl(thumbs);
		if (TryGetThumbnails(channelRenderer, "avatar", out thumbs))
			return ReadLastNonEmptyThumbnailUrl(thumbs);

		if (channelRenderer.TryGetProperty("channelThumbnailSupportedRenderers", out var supportedRenderers) &&
			supportedRenderers.ValueKind == JsonValueKind.Object)
		{
			if (supportedRenderers.TryGetProperty("channelThumbnailWithLinkRenderer", out var withLinkRenderer) &&
				withLinkRenderer.ValueKind == JsonValueKind.Object &&
				TryGetThumbnails(withLinkRenderer, "thumbnail", out thumbs))
			{
				return ReadLastNonEmptyThumbnailUrl(thumbs);
			}

			if (supportedRenderers.TryGetProperty("channelThumbnailWithoutLinkRenderer", out var withoutLinkRenderer) &&
				withoutLinkRenderer.ValueKind == JsonValueKind.Object &&
				TryGetThumbnails(withoutLinkRenderer, "thumbnail", out thumbs))
			{
				return ReadLastNonEmptyThumbnailUrl(thumbs);
			}
		}

		return null;
	}

	static bool TryGetThumbnails(JsonElement parent, string propertyName, out JsonElement thumbnails)
	{
		thumbnails = default;
		if (parent.ValueKind != JsonValueKind.Object)
			return false;
		if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
			return false;
		if (!element.TryGetProperty("thumbnails", out thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
			return false;
		return true;
	}

	static string? ReadLastNonEmptyThumbnailUrl(JsonElement thumbnailsArray)
	{
		if (thumbnailsArray.ValueKind != JsonValueKind.Array)
			return null;

		string? last = null;
		foreach (var thumb in thumbnailsArray.EnumerateArray())
		{
			if (thumb.ValueKind != JsonValueKind.Object) continue;
			if (!thumb.TryGetProperty("url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String) continue;
			var url = urlEl.GetString()?.Trim();
			if (!string.IsNullOrWhiteSpace(url))
				last = NormalizeThumbnailUrl(url);
		}
		return last;
	}

	static string NormalizeThumbnailUrl(string url)
	{
		if (url.StartsWith("//", StringComparison.Ordinal))
			return "https:" + url;

		return url;
	}
}

